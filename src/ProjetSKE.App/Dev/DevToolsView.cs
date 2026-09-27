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
            var status = s.GetQuestStatus(q.Id) switch
            {
                QuestStatus.Active => $"en cours (étape {s.State.Quests[q.Id].Step + 1}/{q.Objectives.Count})",
                QuestStatus.Completed => "terminée",
                _ => "pas commencée",
            };
            questBox.Add(Txt($"{q.Name} — {status}", 13));
            questBox.Add(ButtonRow(
                Form.SmallButton("Démarrer", () => { s.StartQuest(quest.Id); Done(); }),
                Form.SmallButton("Terminer", () => { s.CompleteQuest(quest.Id); Done(); }),
                Form.SmallButton("Oublier", () => { s.ResetQuest(quest.Id); Done(); })));
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
