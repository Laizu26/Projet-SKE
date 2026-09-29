using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Donjon en cours (à la place de la carte) : pas de déplacement, seulement la suite des épreuves.
/// Chaque étape (combat, dialogue, effets) se lance avec « Avancer » ; entre deux, on peut souffler (camp, objets).
/// </summary>
public sealed class DungeonView : ContentView
{
    public DungeonView(GamePage page)
    {
        var s = page.Session;
        var dungeon = s.CurrentDungeon;
        var stack = new VerticalStackLayout { Spacing = 14 };
        if (dungeon is null || s.DungeonStep is not { } step || s.State.Dungeon is not { } run)
        {
            Content = stack;
            return;
        }

        // Bandeau : nom, description, progression (une pastille par épreuve).
        var dots = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center };
        for (var i = 0; i < dungeon.Steps.Count; i++)
        {
            var done = i < run.Step;
            var current = i == run.Step;
            dots.Add(new Border
            {
                WidthRequest = current ? 16 : 11,
                HeightRequest = current ? 16 : 11,
                VerticalOptions = LayoutOptions.Center,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                StrokeThickness = 1.5,
                Stroke = current ? Theme.Gold400 : done ? Theme.Gold600 : Night.Stone600,
                BackgroundColor = done ? Theme.Gold600 : current ? Theme.Gold500.WithAlpha(0.35f) : Colors.Transparent,
            });
        }
        var head = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                IconCaps(Ico.Castle, "Donjon", Theme.Purple600, 10),
                new Label
                {
                    Text = dungeon.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 22, FontAttributes = FontAttributes.Bold,
                    TextColor = Night.Stone100, CharacterSpacing = 3,
                },
            },
        };
        if (dungeon.Description.Length > 0)
            head.Add(new Label { Text = dungeon.Description, FontSize = 13, FontAttributes = FontAttributes.Italic, TextColor = Night.Stone400 });
        head.Add(dots);
        var progress = Caps($"Épreuve {run.Step + 1} / {dungeon.Steps.Count}", 9, Night.Stone400);
        progress.HorizontalOptions = LayoutOptions.Center;
        head.Add(progress);
        stack.Add(DarkCard(head, Ico.Castle, Theme.Purple600, goldLine: true));

        // L'épreuve qui attend.
        var (glyph, kind, color, detail) = step.Type switch
        {
            DungeonStepType.Battle => (Ico.Swords, "Combat", Theme.Red600,
                string.Join(", ", step.MonsterIds.Where(s.Db.Monsters.ContainsKey).Select(id => s.Db.Monsters[id].Name))),
            DungeonStepType.Dialogue => (Ico.MessageCircle, "Rencontre", Theme.Gold700,
                step.DialogueId is { } did && s.Db.Dialogues.TryGetValue(did, out var d) && d.DisplayTitle.Length > 0 ? d.DisplayTitle : ""),
            _ => (Ico.Sparkles, "Découverte", Theme.Green600, ""),
        };
        var info = Stack(Caps(kind, 9, color), Txt(step.Name.Length > 0 ? step.Name : kind, 17, Theme.Text, bold: true));
        info.Spacing = 2;
        if (detail.Length > 0) info.Add(Txt(detail, 13, Theme.Stone600));
        stack.Add(Card(IconRow(IconBox(glyph, color, 48), info)));

        stack.Add(Primary(step.Type == DungeonStepType.Battle ? "Avancer : combattre  ▸" : "Avancer  ▸", page.PlayDungeonStep));
        var leave = Btn("Quitter le donjon", async () =>
        {
            if (await page.DisplayAlertAsync(dungeon.Name, "Quitter le donjon ? Ta progression sera perdue.", "Quitter", "Rester"))
                page.LeaveDungeon();
        });
        stack.Add(leave);
        stack.Add(Muted("Entre deux épreuves, tu peux passer par le camp (équipement, objets) : le donjon t'attend.", 11));
        Content = stack;
    }
}
