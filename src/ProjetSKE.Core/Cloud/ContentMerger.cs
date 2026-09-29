using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Cloud;

/// <summary>Un élément modifié des deux côtés différemment : la version en ligne est gardée, la locale mise de côté.</summary>
public sealed class MergeConflict
{
    /// <summary>Catégorie : « Compétence », « Objet », « PNJ », « Départ »…</summary>
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Version locale (JSON) qui n'a pas été gardée, pour pouvoir la restaurer. Vide = supprimé localement.</summary>
    public string LocalJson { get; set; } = "";
    public DateTime When { get; set; }
}

public sealed record MergeResult(GameContent Merged, IReadOnlyList<MergeConflict> Conflicts, bool HasLocalChanges);

/// <summary>
/// Fusion à trois voies du contenu (version commune, version locale, version en ligne), élément par élément.
/// Principe : ne jamais perdre de travail.
/// <list type="bullet">
/// <item>modifié d'un seul côté → cette version est prise ;</item>
/// <item>modifié des deux côtés de la même façon → rien à faire ;</item>
/// <item>modifié des deux côtés différemment → la version en ligne est gardée, la locale est mise de côté (conflit) ;</item>
/// <item>supprimé d'un côté mais modifié de l'autre → l'élément est gardé (on ne supprime jamais un travail).</item>
/// </list>
/// </summary>
public static class ContentMerger
{
    public static MergeResult Merge(GameContent @base, GameContent local, GameContent remote)
    {
        var conflicts = new List<MergeConflict>();
        var ctx = ContentJsonContext.Default;
        var merged = new GameContent
        {
            Version = Math.Max(local.Version, remote.Version),
            Skills = MergeList("Compétence", @base.Skills, local.Skills, remote.Skills, x => x.Id, x => x.Name, ctx.SkillDef, conflicts),
            Items = MergeList("Objet", @base.Items, local.Items, remote.Items, x => x.Id, x => x.Name, ctx.ItemDef, conflicts),
            Characters = MergeList("PJ", @base.Characters, local.Characters, remote.Characters, x => x.Id, x => x.Name, ctx.CharacterDef, conflicts),
            Monsters = MergeList("Monstre", @base.Monsters, local.Monsters, remote.Monsters, x => x.Id, x => x.Name, ctx.MonsterDef, conflicts),
            Locations = MergeList("Lieu", @base.Locations, local.Locations, remote.Locations, x => x.Id, x => x.Name, ctx.LocationDef, conflicts),
            Npcs = MergeList("PNJ", @base.Npcs, local.Npcs, remote.Npcs, x => x.Id, x => x.Name, ctx.NpcDef, conflicts),
            Dialogues = MergeList("Dialogue", @base.Dialogues, local.Dialogues, remote.Dialogues, x => x.Id, x => x.Name.Length > 0 ? x.Name : x.Id, ctx.DialogueDef, conflicts),
            Quests = MergeList("Quête", @base.Quests, local.Quests, remote.Quests, x => x.Id, x => x.Name, ctx.QuestDef, conflicts),
            Start = MergeValue("Départ", "start", @base.Start, local.Start, remote.Start, ctx.StartSettings, conflicts),
            ExtraStarts = MergeList("Autre départ", @base.ExtraStarts, local.ExtraStarts, remote.ExtraStarts, x => x.Id, x => x.Name, ctx.StartSettings, conflicts),
            Balance = MergeValue("Équilibrage", "balance", @base.Balance, local.Balance, remote.Balance, ctx.BalanceSettings, conflicts),
            Title = MergeValue("Titre", "title", @base.Title, local.Title, remote.Title, ctx.String, conflicts),
            World = MergeValue("Monde", "world", @base.World, local.World, remote.World, ctx.WorldSettings, conflicts),
            Time = MergeValue("Temps", "time", @base.Time, local.Time, remote.Time, ctx.TimeSettings, conflicts),
            Karma = MergeValue("Karma", "karma", @base.Karma, local.Karma, remote.Karma, ctx.ScaleSettings, conflicts),
            Friendship = MergeValue("Amitié", "friendship", @base.Friendship, local.Friendship, remote.Friendship, ctx.ScaleSettings, conflicts),
            Camp = MergeValue("Campement", "camp", @base.Camp, local.Camp, remote.Camp, ctx.CampSettings, conflicts),
            Portraits = MergeList("Portrait", @base.Portraits, local.Portraits, remote.Portraits, x => x.Id, x => x.Name, ctx.PortraitDef, conflicts),
            Variables = MergeList("Variable", @base.Variables, local.Variables, remote.Variables, x => x.Id, x => x.Name, ctx.VariableDef, conflicts),
            Passives = MergeList("Passif", @base.Passives, local.Passives, remote.Passives, x => x.Id, x => x.Name, ctx.PassiveDef, conflicts),
            Gauges = MergeList("Jauge", @base.Gauges, local.Gauges, remote.Gauges, x => x.Id, x => x.Name, ctx.CharacterGaugeDef, conflicts),
            Tutorial = MergeValue("Prologue", "tutorial", @base.Tutorial, local.Tutorial, remote.Tutorial, ctx.TutorialSettings, conflicts),
        };
        var hasLocalChanges = ContentSerializer.ToJson(merged) != ContentSerializer.ToJson(remote);
        return new MergeResult(merged, conflicts, hasLocalChanges);
    }

    private static T MergeValue<T>(string kind, string id, T b, T l, T r, JsonTypeInfo<T> info, List<MergeConflict> conflicts)
    {
        string J(T v) => JsonSerializer.Serialize(v, info);
        var (jb, jl, jr) = (J(b), J(l), J(r));
        if (jl == jr || jl == jb) return r;          // pas de changement local, ou identique
        if (jr == jb) return l;                      // seul le local a changé
        conflicts.Add(new MergeConflict { Kind = kind, Id = id, Name = kind, LocalJson = jl, When = DateTime.Now });
        return r;
    }

    private static List<T> MergeList<T>(string kind, List<T> @base, List<T> local, List<T> remote,
        Func<T, string> id, Func<T, string> name, JsonTypeInfo<T> info, List<MergeConflict> conflicts)
    {
        string J(T v) => JsonSerializer.Serialize(v, info);
        Dictionary<string, T> Index(List<T> list)
        {
            var d = new Dictionary<string, T>();
            foreach (var x in list) d[id(x)] = x;
            return d;
        }
        var b = Index(@base);
        var l = Index(local);
        var r = Index(remote);

        var result = new List<T>();
        var done = new HashSet<string>();

        // Ordre : celui de la version en ligne, puis les nouveautés locales.
        foreach (var key in remote.Select(id).Concat(local.Select(id)))
        {
            if (!done.Add(key)) continue;
            var inB = b.TryGetValue(key, out var vb);
            var inL = l.TryGetValue(key, out var vl);
            var inR = r.TryGetValue(key, out var vr);

            if (inL && inR)
            {
                var (jl, jr) = (J(vl!), J(vr!));
                if (jl == jr) { result.Add(vr!); continue; }
                if (!inB) // créé des deux côtés avec le même identifiant
                {
                    conflicts.Add(new MergeConflict { Kind = kind, Id = key, Name = name(vl!), LocalJson = jl, When = DateTime.Now });
                    result.Add(vr!);
                    continue;
                }
                var jb = J(vb!);
                if (jl == jb) result.Add(vr!);        // seul l'en-ligne a changé
                else if (jr == jb) result.Add(vl!);   // seul le local a changé
                else
                {
                    conflicts.Add(new MergeConflict { Kind = kind, Id = key, Name = name(vl!), LocalJson = jl, When = DateTime.Now });
                    result.Add(vr!);
                }
            }
            else if (inR) // absent en local
            {
                if (!inB) { result.Add(vr!); continue; }                 // ajouté en ligne
                if (J(vr!) == J(vb!)) continue;                          // supprimé en local, inchangé en ligne → supprimé
                result.Add(vr!);                                         // supprimé en local mais modifié en ligne → on garde
                conflicts.Add(new MergeConflict { Kind = kind, Id = key, Name = name(vr!), LocalJson = "", When = DateTime.Now });
            }
            else if (inL) // absent en ligne
            {
                if (!inB) { result.Add(vl!); continue; }                 // ajouté en local
                if (J(vl!) == J(vb!)) continue;                          // supprimé en ligne, inchangé en local → supprimé
                result.Add(vl!);                                         // supprimé en ligne mais modifié en local → on garde
            }
        }
        return result;
    }

    /// <summary>Remet la version locale d'un élément en conflit dans le contenu (sert au bouton « Remettre ma version »).</summary>
    public static bool Restore(GameContent content, MergeConflict conflict)
    {
        var ctx = ContentJsonContext.Default;
        if (conflict.LocalJson.Length == 0) return false;

        bool Put<T>(List<T> list, JsonTypeInfo<T> info, Func<T, string> id)
        {
            var value = JsonSerializer.Deserialize(conflict.LocalJson, info);
            if (value is null) return false;
            var index = list.FindIndex(x => id(x) == conflict.Id);
            if (index >= 0) list[index] = value; else list.Add(value);
            return true;
        }

        switch (conflict.Kind)
        {
            case "Compétence": return Put(content.Skills, ctx.SkillDef, x => x.Id);
            case "Objet": return Put(content.Items, ctx.ItemDef, x => x.Id);
            case "PJ": return Put(content.Characters, ctx.CharacterDef, x => x.Id);
            case "Monstre": return Put(content.Monsters, ctx.MonsterDef, x => x.Id);
            case "Lieu": return Put(content.Locations, ctx.LocationDef, x => x.Id);
            case "PNJ": return Put(content.Npcs, ctx.NpcDef, x => x.Id);
            case "Dialogue": return Put(content.Dialogues, ctx.DialogueDef, x => x.Id);
            case "Quête": return Put(content.Quests, ctx.QuestDef, x => x.Id);
            case "Départ":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.StartSettings) is { } start) { content.Start = start; return true; }
                return false;
            case "Équilibrage":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.BalanceSettings) is { } balance) { content.Balance = balance; return true; }
                return false;
            case "Variable": return Put(content.Variables, ctx.VariableDef, x => x.Id);
            case "Autre départ": return Put(content.ExtraStarts, ctx.StartSettings, x => x.Id);
            case "Portrait": return Put(content.Portraits, ctx.PortraitDef, x => x.Id);
            case "Jauge": return Put(content.Gauges, ctx.CharacterGaugeDef, x => x.Id);
            case "Passif": return Put(content.Passives, ctx.PassiveDef, x => x.Id);
            case "Prologue":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.TutorialSettings) is { } tutorial) { content.Tutorial = tutorial; return true; }
                return false;
            case "Campement":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.CampSettings) is { } camp) { content.Camp = camp; return true; }
                return false;
            case "Monde":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.WorldSettings) is { } world) { content.World = world; return true; }
                return false;
            case "Temps":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.TimeSettings) is { } time) { content.Time = time; return true; }
                return false;
            case "Karma" or "Amitié":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.ScaleSettings) is not { } scale) return false;
                if (conflict.Kind == "Karma") content.Karma = scale; else content.Friendship = scale;
                return true;
            case "Titre":
                if (JsonSerializer.Deserialize(conflict.LocalJson, ctx.String) is { } title) { content.Title = title; return true; }
                return false;
            default: return false;
        }
    }

    /// <summary>Nombre d'éléments différents entre deux contenus (ajoutés, supprimés ou modifiés, réglages compris).</summary>
    public static int CountDifferences(GameContent a, GameContent b)
    {
        var ctx = ContentJsonContext.Default;
        int Lists<T>(List<T> x, List<T> y, Func<T, string> id, JsonTypeInfo<T> info)
        {
            var dx = new Dictionary<string, string>();
            foreach (var item in x) dx[id(item)] = JsonSerializer.Serialize(item, info);
            var dy = new Dictionary<string, string>();
            foreach (var item in y) dy[id(item)] = JsonSerializer.Serialize(item, info);
            return dx.Keys.Union(dy.Keys).Count(k => !dx.TryGetValue(k, out var vx) || !dy.TryGetValue(k, out var vy) || vx != vy);
        }
        int Value<T>(T x, T y, JsonTypeInfo<T> info) => JsonSerializer.Serialize(x, info) == JsonSerializer.Serialize(y, info) ? 0 : 1;
        return Lists(a.Skills, b.Skills, x => x.Id, ctx.SkillDef) + Lists(a.Items, b.Items, x => x.Id, ctx.ItemDef)
            + Lists(a.Characters, b.Characters, x => x.Id, ctx.CharacterDef) + Lists(a.Monsters, b.Monsters, x => x.Id, ctx.MonsterDef)
            + Lists(a.Locations, b.Locations, x => x.Id, ctx.LocationDef) + Lists(a.Npcs, b.Npcs, x => x.Id, ctx.NpcDef)
            + Lists(a.Dialogues, b.Dialogues, x => x.Id, ctx.DialogueDef) + Lists(a.Quests, b.Quests, x => x.Id, ctx.QuestDef)
            + Lists(a.Variables, b.Variables, x => x.Id, ctx.VariableDef) + Lists(a.Portraits, b.Portraits, x => x.Id, ctx.PortraitDef)
            + Lists(a.ExtraStarts, b.ExtraStarts, x => x.Id, ctx.StartSettings)
            + Value(a.Start, b.Start, ctx.StartSettings) + Value(a.Balance, b.Balance, ctx.BalanceSettings)
            + Value(a.Title, b.Title, ctx.String) + Value(a.World, b.World, ctx.WorldSettings) + Value(a.Time, b.Time, ctx.TimeSettings)
            + Value(a.Karma, b.Karma, ctx.ScaleSettings) + Value(a.Friendship, b.Friendship, ctx.ScaleSettings)
            + Value(a.Camp, b.Camp, ctx.CampSettings)
            + Lists(a.Gauges, b.Gauges, x => x.Id, ctx.CharacterGaugeDef) + Lists(a.Passives, b.Passives, x => x.Id, ctx.PassiveDef) + Value(a.Tutorial, b.Tutorial, ctx.TutorialSettings);
    }

    /// <summary>Résumé lisible d'un contenu (pour l'historique).</summary>
    public static string Summary(GameContent c) =>
        $"{c.Characters.Count} PJ · {c.Npcs.Count} PNJ · {c.Dialogues.Count} dialogues · {c.Quests.Count} quêtes · {c.Items.Count} objets · {c.Monsters.Count} monstres · {c.Locations.Count} lieux";

    public static string ConflictsToJson(List<MergeConflict> conflicts) =>
        JsonSerializer.Serialize(conflicts, ContentJsonContext.Default.ListMergeConflict);

    public static List<MergeConflict> ConflictsFromJson(string json) =>
        JsonSerializer.Deserialize(json, ContentJsonContext.Default.ListMergeConflict) ?? [];
}
