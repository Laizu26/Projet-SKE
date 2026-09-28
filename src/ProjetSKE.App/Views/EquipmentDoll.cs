using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Équipement d'un personnage, comme une poupée : la silhouette au centre, reliée à ses emplacements
/// (accessoire, mains, jambes à gauche ; tête, corps, pieds à droite), puis l'arme et le bouclier, et la relique.
/// Toucher un emplacement l'ouvre : pièce portée (retirer) et pièces du sac qui s'y portent (équiper).
/// </summary>
public static class EquipmentDoll
{
    private const double RowHeight = 88;
    private const double Tile = 58;

    private static readonly EquipSlot[] Left = [EquipSlot.Accessory, EquipSlot.Hands, EquipSlot.Legs];
    private static readonly EquipSlot[] Right = [EquipSlot.Head, EquipSlot.Armor, EquipSlot.Feet];

    public static View Build(GamePage page, CharacterState c)
    {
        var s = page.Session;
        var stack = new VerticalStackLayout { Spacing = 14 };

        var doll = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(86)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(86)) },
            RowDefinitions = { new RowDefinition(new GridLength(RowHeight)), new RowDefinition(new GridLength(RowHeight)), new RowDefinition(new GridLength(RowHeight)) },
        };
        for (var i = 0; i < 3; i++)
        {
            doll.Add(SlotTile(page, c, Left[i]), 0, i);
            doll.Add(SlotTile(page, c, Right[i]), 2, i);
        }
        var figure = new GraphicsView
        {
            Drawable = new Figure(Left.Select(x => c.GetEquipped(x) is not null).ToArray(), Right.Select(x => c.GetEquipped(x) is not null).ToArray()),
            HeightRequest = RowHeight * 3,
            InputTransparent = true,
        };
        doll.Add(figure, 1, 0);
        Grid.SetRowSpan(figure, 3);
        stack.Add(doll);

        // Arme et bouclier, puis la relique.
        stack.Add(new HorizontalStackLayout
        {
            Spacing = 36,
            HorizontalOptions = LayoutOptions.Center,
            Children = { SlotTile(page, c, EquipSlot.Weapon), SlotTile(page, c, EquipSlot.Shield) },
        });
        var relic = SlotTile(page, c, EquipSlot.Relic);
        relic.HorizontalOptions = LayoutOptions.Center;
        stack.Add(relic);

        if (page.SelectedSlot is { } open) stack.Add(SlotPanel(page, c, open));
        else stack.Add(Muted("Touchez un emplacement pour changer d'équipement.", 11));

        var card = Card(stack);
        card.Padding = new Thickness(10, 14);
        return card;
    }

    private static View SlotTile(GamePage page, CharacterState c, EquipSlot slot)
    {
        var s = page.Session;
        var item = c.GetEquipped(slot) is { } id && s.Db.Items.TryGetValue(id, out var it) ? it : null;
        var selected = page.SelectedSlot == slot;
        var available = s.Bag().Any(b => b.Item.Slot == slot);
        var box = new Border
        {
            WidthRequest = Tile,
            HeightRequest = Tile,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            StrokeThickness = selected ? 2.5 : 1.5,
            Stroke = selected ? Theme.Gold500 : item is not null ? Theme.Gold700 : Theme.Stone300,
            BackgroundColor = item is not null ? Theme.Stone900 : Theme.Stone100,
            Content = Icon(item is not null ? Theme.ItemIcon(item) : SlotIcon(slot), 24, item is not null ? Theme.Gold400 : Theme.Stone400),
        };
        var top = new Grid { HorizontalOptions = LayoutOptions.Center, Children = { box } };
        if (item is null && available)
        {
            // Pastille : une pièce du sac peut aller ici.
            var dot = new Border
            {
                WidthRequest = 12, HeightRequest = 12, BackgroundColor = Theme.Gold500, StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, TranslationX = 4, TranslationY = -4,
            };
            top.Add(dot);
        }
        var label = Caps(item?.Name ?? SlotName(slot), 8, item is not null ? Theme.Stone800 : Theme.Stone500);
        label.HorizontalTextAlignment = TextAlignment.Center;
        label.LineBreakMode = LineBreakMode.TailTruncation;
        label.MaxLines = 1;
        var tile = new VerticalStackLayout { Spacing = 4, WidthRequest = 84, Children = { top, label } };
        return OnTap(tile, () =>
        {
            page.SelectedSlot = selected ? null : slot;
            page.Render();
        });
    }

    /// <summary>Emplacement ouvert : ce qui est porté, et ce que le sac propose.</summary>
    private static View SlotPanel(GamePage page, CharacterState c, EquipSlot slot)
    {
        var s = page.Session;
        var list = new VerticalStackLayout { Spacing = 10 };
        list.Add(IconCaps(SlotIcon(slot), SlotName(slot), Theme.Gold700, 10));
        var equipped = c.GetEquipped(slot) is { } id && s.Db.Items.TryGetValue(id, out var it) ? it : null;
        if (equipped is not null)
        {
            var info = new VerticalStackLayout { Spacing = 1, Children = { Txt(equipped.Name, 15, Theme.Stone900, bold: true) } };
            if (equipped.Bonus.ToBonusString() is { Length: > 0 } bonus) info.Add(Txt(bonus, 12, Theme.Green600, bold: true));
            list.Add(IconRow(IconBox(Theme.ItemIcon(equipped), Theme.Gold600), info,
                Btn("Retirer", () => { s.Unequip(c, slot); page.AutoSave(); page.Render(); })));
        }
        else list.Add(Muted("Rien de porté.", 12));

        var options = s.Bag().Where(b => b.Item.Slot == slot).ToList();
        foreach (var (item, count) in options)
        {
            var itemId = item.Id;
            var info = new VerticalStackLayout { Spacing = 1, Children = { Txt(count > 1 ? $"{item.Name} x{count}" : item.Name, 14, Theme.Stone900, bold: true) } };
            if (item.Bonus.ToBonusString() is { Length: > 0 } bonus) info.Add(Txt(bonus, 12, Theme.Green600));
            list.Add(IconRow(IconBox(Theme.ItemIcon(item), Theme.Stone700), info,
                Btn("Équiper", () => { s.Equip(c, itemId); page.AutoSave(); page.Render(); })));
        }
        if (options.Count == 0) list.Add(Muted("Aucune pièce de ce type dans le sac.", 12));

        var panel = Card(list, Color.FromArgb("#FEF9C3"), Theme.Gold500);
        panel.Padding = new Thickness(12, 10);
        return panel;
    }

    /// <summary>La silhouette, reliée par des traits à chaque emplacement (dorés quand une pièce est portée).</summary>
    private sealed class Figure(bool[] leftFilled, bool[] rightFilled) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var w = rect.Width;
            var cx = w / 2;
            var scale = (float)(RowHeight * 3 / 264);
            float Y(float v) => v * scale;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            // Points du corps : tête, cou, épaules, mains, bassin, genoux, pieds.
            var head = new PointF(cx, Y(40));
            var neck = new PointF(cx, Y(66));
            var hip = new PointF(cx, Y(150));
            var handL = new PointF(cx - 40, Y(132));
            var handR = new PointF(cx + 40, Y(132));
            var kneeL = new PointF(cx - 16, Y(196));
            var kneeR = new PointF(cx + 16, Y(196));
            var footL = new PointF(cx - 26, Y(240));
            var footR = new PointF(cx + 26, Y(240));

            // Traits vers les emplacements (au niveau du centre de chaque case).
            var rowY = new[] { 30f, 30f + (float)RowHeight, 30f + 2 * (float)RowHeight };
            var leftTargets = new[] { new PointF(cx - 20, Y(52)), handL, kneeL };
            var rightTargets = new[] { new PointF(cx + 22, Y(36)), new PointF(cx + 10, Y(108)), footR };
            for (var i = 0; i < 3; i++)
            {
                Link(canvas, new PointF(0, rowY[i]), leftTargets[i], leftFilled[i]);
                Link(canvas, new PointF(w, rowY[i]), rightTargets[i], rightFilled[i]);
            }

            // Silhouette.
            canvas.StrokeColor = Color.FromArgb("#44403C");
            canvas.StrokeSize = 5;
            canvas.DrawCircle(head, 22 * scale);
            canvas.DrawLine(neck, hip);
            canvas.DrawLine(new PointF(cx, Y(82)), handL);
            canvas.DrawLine(new PointF(cx, Y(82)), handR);
            canvas.DrawLine(hip, kneeL);
            canvas.DrawLine(kneeL, footL);
            canvas.DrawLine(hip, kneeR);
            canvas.DrawLine(kneeR, footR);
            canvas.DrawLine(footL, new PointF(footL.X - 10, footL.Y));
            canvas.DrawLine(footR, new PointF(footR.X + 10, footR.Y));
        }

        private static void Link(ICanvas canvas, PointF from, PointF to, bool filled)
        {
            canvas.StrokeColor = filled ? Color.FromArgb("#CA8A04") : Color.FromArgb("#D6D3D1");
            canvas.StrokeSize = filled ? 2 : 1.5f;
            if (!filled) canvas.StrokeDashPattern = [4, 4];
            canvas.DrawLine(from, to);
            canvas.StrokeDashPattern = null;
            canvas.FillColor = filled ? Color.FromArgb("#EAB308") : Color.FromArgb("#D6D3D1");
            canvas.FillCircle(to, 3.5f);
        }
    }
}
