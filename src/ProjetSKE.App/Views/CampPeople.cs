using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Icônes des tâches du camp (mot-clé choisi dans l'éditeur → glyphe).</summary>
public static class CampIcons
{
    public static readonly IReadOnlyList<(string Key, string Glyph)> All =
    [
        ("ronde", Ico.Footprints), ("chasse", Ico.BowArrow), ("peche", Ico.Fish), ("cueillette", Ico.Wheat),
        ("bois", Ico.Axe), ("forge", Ico.Hammer), ("garde", Ico.Shield), ("entrainement", Ico.Swords),
        ("repos", Ico.Moon), ("cuisine", Ico.Flame), ("eclaireur", Ico.Eye), ("commerce", Ico.Coins),
        ("mine", Ico.Pickaxe), ("construction", Ico.HardHat), ("soin", Ico.Heart), ("etude", Ico.BookOpen),
    ];

    public static string Get(string key) => All.FirstOrDefault(i => i.Key == key).Glyph ?? Ico.ClipboardList;
}

/// <summary>
/// Le camp : la hiérarchie (le chef, puis chaque grade), ce que fait chacun, et le journal du camp.
/// Toucher un membre : sa fiche, pour changer son grade, l'affecter à une tâche ou lui parler.
/// </summary>
public static class CampPeople
{
    /// <summary>Gestion : hiérarchie, répartition des tâches, et journal du camp.</summary>
    public static void Build(VerticalStackLayout stack, GamePage page)
    {
        var s = page.Session;
        if (page.SelectedCampMember is { } id && s.CampMember(id) is { } selected)
        {
            BuildMember(stack, page, selected);
            return;
        }
        if (!s.Db.Content.Time.Enabled)
            stack.Add(IconRow(Icon(Ico.Hourglass, 14, Theme.Stone500), Muted("Le temps est arrêté : les tâches n'avancent pas.")));
        Hierarchy(stack, page);
        Tasks(stack, page);
        Available(stack, page);
        Log(stack, page);
    }

    /// <summary>Persos en liste (quand ils sont trop nombreux pour les cercles, ou au choix).</summary>
    public static void BuildList(VerticalStackLayout stack, GamePage page)
    {
        Hierarchy(stack, page);
        Available(stack, page);
    }

    /// <summary>Qui fait quoi : chaque tâche débloquée et ses membres, puis ceux au repos.</summary>
    private static void Tasks(VerticalStackLayout stack, GamePage page)
    {
        var s = page.Session;
        var tasks = s.CampRules.Tasks.Where(t => s.CheckAll(t.Conditions) || s.CampMembers.Any(m => m.TaskId == t.Id)).ToList();
        if (tasks.Count == 0) return;
        stack.Add(Section("Tâches"));
        var list = new VerticalStackLayout { Spacing = 8 };
        foreach (var task in tasks)
        {
            var workers = s.CampMembers.Where(m => m.TaskId == task.Id).Select(m => s.CharacterName(m.Id)).ToList();
            var count = task.MaxWorkers > 0 ? $"{workers.Count}/{task.MaxWorkers}" : workers.Count.ToString();
            list.Add(IconRow(Icon(CampIcons.Get(task.Icon), 16, workers.Count > 0 ? Theme.Gold700 : Theme.Stone400),
                new VerticalStackLayout
                {
                    Spacing = 1,
                    Children =
                    {
                        Txt(task.Name, 14, Theme.Stone900, bold: true),
                        Txt(workers.Count > 0 ? string.Join(", ", workers) : "Personne", 12, workers.Count > 0 ? Theme.Stone700 : Theme.Stone400),
                    },
                }, Badge(count, workers.Count > 0 ? Theme.Gold700 : Theme.Stone500)));
        }
        var idle = s.CampMembers.Where(m => m.TaskId is null).Select(m => s.CharacterName(m.Id)).ToList();
        if (idle.Count > 0)
            list.Add(IconRow(Icon(Ico.Moon, 16, Theme.Stone400), new VerticalStackLayout
            {
                Spacing = 1,
                Children = { Txt(s.Db.T("camp.rest"), 14, Theme.Stone900, bold: true), Txt(string.Join(", ", idle), 12, Theme.Stone500) },
            }, Badge(idle.Count.ToString(), Theme.Stone500)));
        list.Add(Muted("Touchez un membre dans la hiérarchie pour changer sa tâche.", 11));
        var card = Card(list);
        card.Padding = new Thickness(12, 10);
        stack.Add(card);
    }

    private static void Hierarchy(VerticalStackLayout stack, GamePage page)
    {
        var s = page.Session;
        var rules = s.CampRules;

        // Le chef : le héros.
        var hero = s.State.Party.FirstOrDefault(c => c.DefId == s.State.HeroId);
        if (hero is not null)
        {
            var name = s.DefOf(hero).Name;
            stack.Add(DarkCard(IconRow(Face(page, hero.DefId, name, 48, Theme.Gold500),
                new VerticalStackLayout
                {
                    Spacing = 2,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { Caps(rules.LeaderTitle, 9, Theme.Gold500), Txt(name, 18, Theme.Stone100, bold: true) },
                }), Ico.Crown));
        }

        // Chaque grade, du plus haut au plus bas.
        var members = s.CampMembers;
        foreach (var rank in rules.Ranks.OrderByDescending(r => r.Level))
        {
            var inRank = members.Where(m => m.RankId == rank.Id).ToList();
            var slots = rank.Max > 0 ? $"  {inRank.Count}/{rank.Max}" : $"  {inRank.Count}";
            stack.Add(Section(rank.Name + slots));
            if (inRank.Count == 0) stack.Add(Muted("Personne."));
            foreach (var m in inRank) stack.Add(MemberCard(page, m));
        }
        var unranked = members.Where(m => !rules.Ranks.Any(r => r.Id == m.RankId)).ToList();
        if (unranked.Count > 0)
        {
            stack.Add(Section("Sans grade"));
            foreach (var m in unranked) stack.Add(MemberCard(page, m));
        }
    }

    private static void Available(VerticalStackLayout stack, GamePage page)
    {
        var s = page.Session;
        // PJ en réserve pas encore au camp.
        var available = s.State.Party.Where(c => !c.IsActive && s.CampMember(c.DefId) is null && c.DefId != s.State.HeroId).ToList();
        if (available.Count > 0)
        {
            stack.Add(Section(s.Db.T("camp.available")));
            foreach (var c in available)
            {
                var cid = c.DefId;
                stack.Add(Card(IconRow(Face(page, cid, s.DefOf(c).Name, 40, Theme.AvatarColor(cid)),
                    Txt(s.DefOf(c).Name, 15, Theme.Stone900, bold: true),
                    Btn("Intégrer au camp", () => { s.JoinCamp(cid); page.Render(); }))));
            }
        }
    }

    private static void Log(VerticalStackLayout stack, GamePage page)
    {
        var s = page.Session;
        // Journal du camp.
        stack.Add(Section(s.Db.T("camp.log")));
        if (s.State.CampLog.Count == 0) stack.Add(Muted("Rien pour l'instant. Affectez les membres à des tâches, puis laissez passer le temps."));
        else
        {
            var log = new VerticalStackLayout { Spacing = 6 };
            foreach (var line in Enumerable.Reverse(s.State.CampLog).Take(12))
                log.Add(IconRow(Icon(Ico.Feather, 12, Theme.Gold600), Txt(line, 12, Theme.Stone700)));
            var card = Card(log);
            card.Padding = new Thickness(12, 10);
            stack.Add(card);
        }
    }

    /// <summary>Portrait (banque d'images) si le PNJ/PJ en a un, sinon pastille avec l'initiale.</summary>
    public static View Face(GamePage page, string id, string name, double size, Color color)
    {
        var db = page.Session.Db;
        var portraitId = db.Npcs.TryGetValue(id, out var npc) ? npc.PortraitId : db.Characters.TryGetValue(id, out var pc) ? pc.PortraitId : null;
        if (portraitId is not null && db.Portraits.TryGetValue(portraitId, out var portrait))
        {
            return new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = size / 2 },
                Stroke = color,
                StrokeThickness = 2,
                Content = new FramedImage(portrait),
            };
        }
        return Avatar(name, color, size);
    }

    private static CampTaskDef? TaskOf(GameSession s, CampMemberState m) =>
        m.TaskId is { } t ? s.CampRules.Tasks.FirstOrDefault(x => x.Id == t) : null;

    private static View MemberCard(GamePage page, CampMemberState m)
    {
        var s = page.Session;
        var name = s.CharacterName(m.Id);
        var task = TaskOf(s, m);
        var info = new VerticalStackLayout { Spacing = 3 };
        info.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { Txt(name, 15, Theme.Stone900, bold: true), Badge(s.RankOf(m)?.Name ?? "—", Theme.Stone600) },
        });
        if (task is null)
        {
            info.Add(IconRow(Icon(Ico.Moon, 12, Theme.Stone400), Caps(s.Db.T("camp.rest"), 9, Theme.Stone400)));
        }
        else
        {
            info.Add(IconRow(Icon(CampIcons.Get(task.Icon), 12, Theme.Gold700), Caps(task.Name, 9, Theme.Gold700)));
            var duration = Math.Max(1, task.DurationMinutes);
            var done = (int)Math.Clamp(duration - (m.NextAt - s.State.Minutes), 0, duration);
            info.Add(Bar("", done, duration, Theme.Gold500, 5));
        }
        var friendship = s.Db.Content.Friendship;
        if (friendship.Enabled && friendship.Visible)
        {
            var value = s.GetFriendship(m.Id);
            info.Add(Caps($"{friendship.Name} {value} {friendship.TierName(value)}".Trim(), 8, Theme.Stone500));
        }
        var card = Card(IconRow(Face(page, m.Id, name, 44, Theme.AvatarColor(m.Id)), info, Icon(Ico.ChevronRight, 18, Theme.Stone400)));
        card.Padding = new Thickness(12, 10);
        var id = m.Id;
        return OnTap(card, () => { page.SelectedCampMember = id; page.Render(); });
    }

    private static void BuildMember(VerticalStackLayout stack, GamePage page, CampMemberState m)
    {
        var s = page.Session;
        var name = s.CharacterName(m.Id);
        var id = m.Id;
        stack.Add(Pill("◂  " + s.Db.T(page.CampSection == CampSection.People ? "camp.people" : "camp.manage"),
            () => { page.SelectedCampMember = null; page.Render(); }));

        var description = s.Db.Npcs.TryGetValue(id, out var npc) ? npc.Description : s.Db.Characters.TryGetValue(id, out var pc) ? pc.Description : "";
        var head = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                Face(page, id, name, 80, Theme.Gold500),
                new Label
                {
                    Text = name.ToUpperInvariant(), FontFamily = "serif", FontSize = 22, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Stone100, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = (s.RankOf(m)?.Name ?? "Sans grade").ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Gold500, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center,
                },
            },
        };
        if (description.Length > 0)
            head.Add(new Label { Text = description, FontSize = 13, FontAttributes = FontAttributes.Italic, TextColor = Theme.Stone400, HorizontalTextAlignment = TextAlignment.Center });
        stack.Add(DarkCard(head, Ico.Users, goldLine: true));

        if (npc is not null && (npc.DefaultDialogueId is not null || npc.ConditionalDialogues.Count > 0))
            stack.Add(Primary($"Parler à {name}", () => page.TalkTo(id)));

        // Grade.
        stack.Add(Section("Grade"));
        var ranks = s.CampRules.Ranks.OrderByDescending(r => r.Level).Select(r =>
        {
            var rank = r;
            var free = s.FreeSlots(r);
            var label = free is null || r.Id == m.RankId ? r.Name : $"{r.Name} ({free} place{(free > 1 ? "s" : "")})";
            return (View)Btn(label, () => { s.SetCampRank(id, rank.Id); page.AutoSave(); page.Render(); },
                enabled: r.Id == m.RankId || free != 0, selected: r.Id == m.RankId);
        }).ToList();
        if (ranks.Count > 0) stack.Add(TileGrid(ranks, 2));
        else stack.Add(Muted("Aucun grade défini (mode développeur › Campement)."));

        // Tâche.
        stack.Add(Section("Tâche"));
        stack.Add(Btn(s.Db.T("camp.rest"), () => { s.SetCampTask(id, null); page.AutoSave(); page.Render(); }, selected: m.TaskId is null));
        foreach (var task in s.CampRules.Tasks)
        {
            var t = task;
            var reason = s.CannotTake(m, task);
            var current = m.TaskId == task.Id;
            if (reason is not null && !current && !s.CheckAll(task.Conditions)) continue; // tâche pas encore débloquée : cachée
            var info = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Txt(task.Name, 15, Theme.Stone900, bold: true),
                    Caps(Duration(task.DurationMinutes) + (task.MaxWorkers > 0 ? $" · {task.MaxWorkers} max" : ""), 8, Theme.Stone500),
                },
            };
            if (task.Description.Length > 0) info.Add(Txt(task.Description, 12, Theme.Stone600));
            if (reason is not null && !current) info.Add(IconRow(Icon(Ico.Lock, 11, Theme.Red600), Txt(reason, 11, Theme.Red600)));
            var card = Card(IconRow(IconBox(CampIcons.Get(task.Icon), current ? Theme.Gold600 : Theme.Stone700), info),
                current ? Color.FromArgb("#FEF9C3") : null, current ? Theme.Gold500 : null);
            card.Padding = new Thickness(12, 10);
            if (reason is null && !current) OnTap(card, () => { s.SetCampTask(id, t.Id); page.AutoSave(); page.Render(); });
            else if (reason is not null) card.Opacity = 0.55;
            stack.Add(card);
        }

        if (npc is not null)
            stack.Add(Btn("Renvoyer du camp", () => { s.LeaveCamp(id); page.SelectedCampMember = null; page.AutoSave(); page.Render(); }));
    }

    private static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes} min" : minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60:00}";
}
