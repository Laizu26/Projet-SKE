using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>Outils de test pendant une partie (menu, mode développeur déverrouillé).</summary>
public sealed class DevToolsView : ContentView
{
    private readonly GamePage _page;

    public DevToolsView(GamePage page)
    {
        _page = page;
        var s = page.Session;
        var db = s.Db;
        var stack = Stack();

        // Téléportation
        var locations = db.Content.Locations;
        var locPicker = MakePicker(locations.Select(l => l.Name).ToList());
        stack.Add(Panel(Stack(Muted("Se téléporter"), locPicker, Btn("Y aller", () =>
        {
            if (locPicker.SelectedIndex < 0) return;
            s.Execute(new GameAction(ActionType.Teleport, locations[locPicker.SelectedIndex].Id));
            Done();
        }))));

        // Or, XP, soin
        stack.Add(Panel(Stack(
            Muted("Ressources"),
            ButtonRow(
                Btn("+100 or", () => { s.State.Gold += 100; Done(); }),
                Btn("+1000 or", () => { s.State.Gold += 1000; Done(); })),
            ButtonRow(
                Btn("+50 XP", () => { s.Execute(new GameAction(ActionType.GiveXp, amount: 50)); Done(); }),
                Btn("+500 XP", () => { s.Execute(new GameAction(ActionType.GiveXp, amount: 500)); Done(); })),
            Btn("Soigner l'équipe", () => { s.HealAll(); Done("Équipe soignée."); }))));

        // PV du héros (pour voir l'écran se fissurer)
        if (s.State.Party.FirstOrDefault(c => c.DefId == s.State.HeroId) is { } hero)
        {
            void SetHp(int percent)
            {
                hero.CurrentHp = Math.Max(1, s.GetStats(hero).MaxHp * percent / 100);
                Done();
            }
            stack.Add(Panel(Stack(
                Muted($"PV du héros : {s.HeroHpPercent} % (écran fissuré)"),
                ButtonRow(
                    Btn("100 %", () => SetHp(100)),
                    Btn("25 %", () => SetHp(25)),
                    Btn("10 %", () => SetHp(10)),
                    Btn("3 %", () => SetHp(3))))));
        }

        // Temps
        var clock = s.Clock;
        stack.Add(Panel(Stack(
            Muted($"Temps : {clock.DateText} · {clock.TimeText} · {clock.Period}"),
            ButtonRow(
                Btn("+1 h", () => { s.AdvanceTime(60); Done(); }),
                Btn("+6 h", () => { s.AdvanceTime(360); Done(); }),
                Btn("+1 jour", () => { s.AdvanceTime(60L * Math.Max(1, db.Content.Time.HoursPerDay)); Done(); })))));

        // Karma de chaque PJ
        var karmaBox = Stack(Muted(db.Content.Karma.Name));
        foreach (var c in s.State.Party)
        {
            var id = c.DefId;
            karmaBox.Add(Row(Txt($"{s.DefOf(c).Name} : {c.Karma} {db.Content.Karma.TierName(c.Karma)}", 13), ButtonRow(
                Form.SmallButton("-10", () => { s.Execute(new GameAction(ActionType.AddKarma, id, -10)); Done(); }),
                Form.SmallButton("+10", () => { s.Execute(new GameAction(ActionType.AddKarma, id, 10)); Done(); }))));
        }
        stack.Add(Panel(karmaBox));

        // Jauges de chaque PJ (folie...)
        foreach (var gauge in db.Content.Gauges)
        {
            var g = gauge;
            var box = Stack(Muted(g.Name));
            foreach (var c in s.State.Party)
            {
                var id = c.DefId;
                var value = s.GaugeOf(c, g);
                box.Add(Row(Txt($"{s.DefOf(c).Name} : {value} {g.TierName(value)}", 13), ButtonRow(
                    Form.SmallButton("-10", () => { s.Execute(new GameAction(ActionType.AddGauge, g.Id, -10) { Arg2 = id }); Done(); }),
                    Form.SmallButton("+10", () => { s.Execute(new GameAction(ActionType.AddGauge, g.Id, 10) { Arg2 = id }); Done(); }))));
            }
            stack.Add(Panel(box));
        }

        // Variables
        if (db.Content.Variables.Count > 0)
        {
            var varBox = Stack(Muted("Variables"));
            foreach (var v in db.Content.Variables)
            {
                var id = v.Id;
                varBox.Add(Row(Txt($"{v.Name} : {s.GetVariable(id)}", 13), ButtonRow(
                    Form.SmallButton("-1", () => { s.SetVariable(id, s.GetVariable(id) - 1); Done(); }),
                    Form.SmallButton("+1", () => { s.SetVariable(id, s.GetVariable(id) + 1); Done(); }))));
            }
            stack.Add(Panel(varBox));
        }

        // Objets
        var items = db.Content.Items;
        var itemPicker = MakePicker(items.Select(i => $"{i.Name} ({DevState.Name(i.Type)})").ToList());
        stack.Add(Panel(Stack(Muted("Donner un objet"), itemPicker, Btn("Donner", () =>
        {
            if (itemPicker.SelectedIndex < 0) return;
            s.Execute(new GameAction(ActionType.GiveItem, items[itemPicker.SelectedIndex].Id));
            Done();
        }))));

        // Recruter
        var chars = db.Content.Characters.Where(c => !s.IsInParty(c.Id)).ToList();
        if (chars.Count > 0)
        {
            var charPicker = MakePicker(chars.Select(c => c.Name).ToList());
            stack.Add(Panel(Stack(Muted("Recruter un PJ"), charPicker, Btn("Recruter", () =>
            {
                if (charPicker.SelectedIndex < 0) return;
                s.Execute(new GameAction(ActionType.Recruit, chars[charPicker.SelectedIndex].Id));
                Done();
            }))));
        }

        // Combat
        var monsters = db.Content.Monsters;
        var monsterPicker = MakePicker(monsters.Select(m => m.Name + (m.IsBoss ? " (boss)" : "")).ToList());
        var countPicker = MakePicker(["×1", "×2", "×3", "×4"]);
        countPicker.SelectedIndex = 0;
        stack.Add(Panel(Stack(Muted("Lancer un combat"), monsterPicker, countPicker, Btn("Combattre", () =>
        {
            if (monsterPicker.SelectedIndex < 0) return;
            var id = monsters[monsterPicker.SelectedIndex].Id;
            page.StartBattle(Enumerable.Repeat(id, countPicker.SelectedIndex + 1).ToList());
        }))));

        // Dialogue
        var dialogues = db.Content.Dialogues;
        var dialoguePicker = MakePicker(dialogues.Select(d => d.Name.Length > 0 ? d.Name : d.Id).ToList());
        stack.Add(Panel(Stack(Muted("Lancer un dialogue"), dialoguePicker, Btn("Jouer", () =>
        {
            if (dialoguePicker.SelectedIndex < 0) return;
            page.ShowDialogue(dialogues[dialoguePicker.SelectedIndex].Id);
        }))));

        // Quêtes
        var questBox = Stack(Muted("Quêtes"));
        foreach (var q in db.Content.Quests)
        {
            var quest = q;
            var progress = s.QuestProgressOf(q.Id);
            var stageName = progress is not null && s.CurrentStage(q, progress) is { } cs ? cs.Name : null;
            var status = s.GetQuestStatus(q.Id) switch
            {
                QuestStatus.Active when q.IsStaged => $"en cours : {stageName}",
                QuestStatus.Active when q.HasParts => $"en cours ({q.Parts.Count(p => s.GetPartStatus(q.Id, p.Id) == QuestStatus.Completed)}/{q.Parts.Count} parties terminées)",
                QuestStatus.Active => $"en cours (objectif {progress!.Step + 1}/{q.Objectives.Count})",
                QuestStatus.Completed => "terminée" + (stageName is not null ? $" ({stageName})" : ""),
                QuestStatus.Failed => "échouée" + (stageName is not null ? $" ({stageName})" : ""),
                _ => "pas commencée",
            };
            questBox.Add(Txt($"{q.Name} — {status}", 13));
            questBox.Add(ButtonRow(
                Form.SmallButton("Démarrer", () => { s.StartQuest(quest.Id); Done(); }),
                Form.SmallButton("Terminer", () => { s.CompleteQuest(quest.Id); Done(); }),
                Form.SmallButton("Oublier", () => { s.ResetQuest(quest.Id); Done(); })));
            if (q.HasParts)
            {
                // Chaque partie : son état, et de quoi le forcer (pour tester ce qui en dépend).
                foreach (var part in q.Parts)
                {
                    var p = part;
                    var partStatus = s.GetPartStatus(q.Id, p.Id);
                    var (glyph, color, label) = partStatus switch
                    {
                        QuestStatus.Active => (Ico.Target, Theme.Gold600, "en cours"),
                        QuestStatus.Completed => (Ico.CircleCheck, Theme.Good, "terminée"),
                        QuestStatus.Failed => (Ico.X, Theme.Danger, "échouée"),
                        _ => (Ico.Lock, Theme.Muted, "pas commencée"),
                    };
                    questBox.Add(IconRow(Icon(glyph, 13, color), Txt($"{(p.Name.Length > 0 ? p.Name : p.Id)} ({p.Id}) — {label}{(p.Optional ? " · facultative" : "")}", 12)));
                    questBox.Add(ButtonRow(
                        Form.SmallButton("Démarrer", () => { s.StartPart(quest.Id, p.Id); Done(); }),
                        Form.SmallButton("Terminer", () => { s.CompletePart(quest.Id, p.Id); Done(); }),
                        Form.SmallButton("Échouer", () => { s.FailPart(quest.Id, p.Id); Done(); })));
                }
            }
            if (q.IsStaged)
            {
                var stagePicker = MakePicker(q.Stages.Select(st => st.Name.Length > 0 ? st.Name : st.Id).ToList());
                stagePicker.Title = "Aller à l'étape...";
                stagePicker.SelectedIndexChanged += (_, _) =>
                {
                    if (stagePicker.SelectedIndex < 0) return;
                    s.ResetQuest(quest.Id);
                    s.GoToStage(quest.Id, quest.Stages[stagePicker.SelectedIndex].Id);
                    Done();
                };
                questBox.Add(stagePicker);
            }
        }
        stack.Add(Panel(questBox));

        // Flags
        var flagBox = Stack(Muted($"Flags ({s.State.Flags.Count})"));
        foreach (var flag in s.State.Flags.OrderBy(f => f).ToList())
        {
            var fl = flag;
            flagBox.Add(Row(Txt(flag, 12), Form.SmallButton("✕", () => { s.State.Flags.Remove(fl); Done(); })));
        }
        var flagEntry = new Entry { Placeholder = "nouveau_flag", PlaceholderColor = Theme.Muted, TextColor = Theme.Text, BackgroundColor = Theme.Panel };
        flagBox.Add(Row(flagEntry, Form.SmallButton("Poser", () =>
        {
            if (string.IsNullOrWhiteSpace(flagEntry.Text)) return;
            s.SetFlag(flagEntry.Text.Trim());
            Done();
        })));
        stack.Add(Panel(flagBox));

        Content = stack;
    }

    private static Picker MakePicker(List<string> items) => new()
    {
        ItemsSource = items,
        Title = "Choisir...",
        TextColor = Theme.Text,
        TitleColor = Theme.Muted,
        BackgroundColor = Theme.Panel,
    };

    private void Done(string? message = null)
    {
        if (message is not null) _page.Notify(message);
        _page.Render();
    }
}
