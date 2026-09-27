using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>Registre de tout le contenu du jeu, indexé par identifiant.</summary>
public sealed class GameDatabase
{
    public IReadOnlyDictionary<string, SkillDef> Skills { get; }
    public IReadOnlyDictionary<string, ItemDef> Items { get; }
    public IReadOnlyDictionary<string, CharacterDef> Characters { get; }
    public IReadOnlyDictionary<string, MonsterDef> Monsters { get; }
    public IReadOnlyDictionary<string, LocationDef> Locations { get; }
    public IReadOnlyDictionary<string, DialogueDef> Dialogues { get; }

    /// <summary>Lieu de départ d'une nouvelle partie.</summary>
    public string StartLocationId { get; }
    public int StartGold { get; }
    public IReadOnlyDictionary<string, int> StartInventory { get; }
    public string? IntroDialogueId { get; }

    public GameDatabase(
        IEnumerable<SkillDef> skills,
        IEnumerable<ItemDef> items,
        IEnumerable<CharacterDef> characters,
        IEnumerable<MonsterDef> monsters,
        IEnumerable<LocationDef> locations,
        IEnumerable<DialogueDef> dialogues,
        string startLocationId,
        int startGold,
        IReadOnlyDictionary<string, int> startInventory,
        string? introDialogueId)
    {
        Skills = skills.ToDictionary(s => s.Id);
        Items = items.ToDictionary(i => i.Id);
        Characters = characters.ToDictionary(c => c.Id);
        Monsters = monsters.ToDictionary(m => m.Id);
        Locations = locations.ToDictionary(l => l.Id);
        Dialogues = dialogues.ToDictionary(d => d.Id);
        StartLocationId = startLocationId;
        StartGold = startGold;
        StartInventory = startInventory;
        IntroDialogueId = introDialogueId;
    }

    private static GameDatabase? _default;

    /// <summary>Contenu d'exemple fourni avec le jeu.</summary>
    public static GameDatabase Default => _default ??= SampleContent.Build();

    public IEnumerable<CharacterDef> Starters => Characters.Values.Where(c => c.IsStarter);

    /// <summary>Vérifie que toutes les références entre contenus existent. Renvoie la liste des erreurs.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        void Check(bool ok, string message) { if (!ok) errors.Add(message); }

        foreach (var c in Characters.Values)
        {
            foreach (var s in c.Skills) Check(Skills.ContainsKey(s.SkillId), $"Personnage {c.Id} : compétence {s.SkillId} inconnue");
            if (c.StartingWeaponId is { } w) Check(Items.ContainsKey(w), $"Personnage {c.Id} : arme {w} inconnue");
            if (c.StartingArmorId is { } a) Check(Items.ContainsKey(a), $"Personnage {c.Id} : armure {a} inconnue");
        }
        foreach (var m in Monsters.Values)
        {
            foreach (var s in m.SkillIds) Check(Skills.ContainsKey(s), $"Monstre {m.Id} : compétence {s} inconnue");
            foreach (var d in m.Drops) Check(Items.ContainsKey(d.ItemId), $"Monstre {m.Id} : butin {d.ItemId} inconnu");
            Check(m.SkillIds.Count > 0, $"Monstre {m.Id} : aucune compétence");
        }
        foreach (var l in Locations.Values)
        {
            foreach (var id in l.ConnectedIds) Check(Locations.ContainsKey(id), $"Lieu {l.Id} : lieu relié {id} inconnu");
            foreach (var id in l.ShopItemIds) Check(Items.ContainsKey(id), $"Lieu {l.Id} : article {id} inconnu");
            foreach (var g in l.RandomEncounters)
                foreach (var id in g.MonsterIds) Check(Monsters.ContainsKey(id), $"Lieu {l.Id} : monstre {id} inconnu");
            if (l.FixedBattle is { } fb)
            {
                foreach (var id in fb.MonsterIds) Check(Monsters.ContainsKey(id), $"Lieu {l.Id} : monstre {id} inconnu");
                if (fb.IntroDialogueId is { } d) Check(Dialogues.ContainsKey(d), $"Lieu {l.Id} : dialogue {d} inconnu");
            }
            if (l.FirstVisitDialogueId is { } fv) Check(Dialogues.ContainsKey(fv), $"Lieu {l.Id} : dialogue {fv} inconnu");
            foreach (var n in l.Npcs) Check(Dialogues.ContainsKey(n.DialogueId), $"Lieu {l.Id} : dialogue {n.DialogueId} inconnu");
        }
        foreach (var d in Dialogues.Values)
        {
            var ids = d.Nodes.Select(n => n.Id).ToHashSet();
            foreach (var n in d.Nodes)
            {
                if (n.NextId is { } next) Check(ids.Contains(next), $"Dialogue {d.Id} : nœud {next} inconnu");
                foreach (var ch in n.Choices)
                {
                    if (ch.NextId is { } cn) Check(ids.Contains(cn), $"Dialogue {d.Id} : nœud {cn} inconnu");
                    CheckActions(ch.Actions, d.Id);
                }
                CheckActions(n.Actions, d.Id);
            }
        }
        Check(Locations.ContainsKey(StartLocationId), $"Lieu de départ {StartLocationId} inconnu");
        foreach (var id in StartInventory.Keys) Check(Items.ContainsKey(id), $"Inventaire de départ : {id} inconnu");
        if (IntroDialogueId is { } intro) Check(Dialogues.ContainsKey(intro), $"Dialogue d'intro {intro} inconnu");
        Check(Starters.Any(), "Aucun personnage de départ");
        return errors;

        void CheckActions(IEnumerable<DialogueAction> actions, string dialogueId)
        {
            foreach (var a in actions)
            {
                switch (a.Type)
                {
                    case DialogueActionType.Recruit:
                        Check(Characters.ContainsKey(a.Arg), $"Dialogue {dialogueId} : personnage {a.Arg} inconnu");
                        break;
                    case DialogueActionType.GiveItem:
                        Check(Items.ContainsKey(a.Arg), $"Dialogue {dialogueId} : objet {a.Arg} inconnu");
                        break;
                    case DialogueActionType.StartBattle:
                        foreach (var id in a.Arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            Check(Monsters.ContainsKey(id), $"Dialogue {dialogueId} : monstre {id} inconnu");
                        break;
                }
            }
        }
    }
}
