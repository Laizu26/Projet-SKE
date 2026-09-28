using Microsoft.Maui.Controls.Shapes;
using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Cercle du campement : un disque sombre, le feu (animé) au centre, et des éléments posés en cercle autour.
/// Les positions sont proportionnelles : 0 = centre, 1 = bord du disque.
/// </summary>
public sealed class CampRing : ContentView
{
    private const double MaxSide = 440;
    private readonly AbsoluteLayout _layout = new();

    public CampRing(double initialSide = 340)
    {
        _layout.WidthRequest = _layout.HeightRequest = initialSide;
        _layout.HorizontalOptions = LayoutOptions.Center;

        // Disque : lueur chaude au centre, nuit tout autour.
        var disc = new Ellipse
        {
            Fill = new RadialGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromArgb("#5C3A12"), 0f),
                new GradientStop(Color.FromArgb("#2A1D10"), 0.45f),
                new GradientStop(Theme.Stone950, 1f),
            }),
            Stroke = Theme.Gold700,
            StrokeThickness = 1.5,
        };
        Fill(disc, 1);
        Content = new Grid { Children = { _layout } };
        SizeChanged += (_, _) =>
        {
            if (Width <= 0) return;
            var side = Math.Min(Width, MaxSide);
            if (Math.Abs(_layout.WidthRequest - side) > 1) _layout.WidthRequest = _layout.HeightRequest = side;
        };
    }

    /// <summary>Anneau décoratif (pointillé) de diamètre relatif donné.</summary>
    public void Circle(double diameter, Color color, bool dashed = true)
    {
        var ring = new Ellipse { Stroke = color, StrokeThickness = 1, Opacity = 0.6, InputTransparent = true };
        if (dashed) ring.StrokeDashArray = new DoubleCollection { 4, 4 };
        Fill(ring, diameter);
    }

    private void Fill(View view, double size)
    {
        AbsoluteLayout.SetLayoutFlags(view, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(view, new Rect(0.5, 0.5, size, size));
        _layout.Add(view);
    }

    /// <summary>Pose un élément à l'angle donné (degrés, 0 = droite, -90 = haut) et à la distance relative donnée.</summary>
    public void Place(View view, double angle, double distance, double width, double height)
    {
        var rad = angle * Math.PI / 180;
        var x = 0.5 + 0.5 * distance * Math.Cos(rad);
        var y = 0.5 + 0.5 * distance * Math.Sin(rad);
        AbsoluteLayout.SetLayoutFlags(view, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(view, new Rect(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1), width, height));
        _layout.Add(view);
    }

    /// <summary>Le feu au centre : une lueur qui palpite et des flammes qui dansent.</summary>
    public void Fire(double size = 110, View? onTop = null)
    {
        var glow = new Ellipse
        {
            Fill = new RadialGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromArgb("#CCF59E0B"), 0f),
                new GradientStop(Color.FromArgb("#55EA580C"), 0.5f),
                new GradientStop(Colors.Transparent, 1f),
            }),
            InputTransparent = true,
        };
        var outer = Icon(Ico.Flame, size * 0.46, Color.FromArgb("#F97316"));
        var inner = Icon(Ico.Flame, size * 0.26, Theme.Gold400);
        inner.TranslationY = size * 0.06;
        foreach (var l in new[] { outer, inner })
        {
            l.HorizontalOptions = LayoutOptions.Center;
            l.VerticalOptions = LayoutOptions.Center;
            l.AnchorY = 1;
        }
        var fire = new Grid { InputTransparent = onTop is null, Children = { glow, outer, inner } };
        if (onTop is not null)
        {
            // Au centre, quelqu'un (le chef) devant le feu : les flammes restent en fond.
            outer.Opacity = inner.Opacity = 0.35;
            onTop.HorizontalOptions = LayoutOptions.Center;
            onTop.VerticalOptions = LayoutOptions.Center;
            fire.Add(onTop);
        }
        Place(fire, 0, 0, size, size);

        fire.Loaded += (_, _) =>
        {
            var flicker = new Animation
            {
                { 0, 0.5, new Animation(v => outer.ScaleY = v, 0.92, 1.1, Easing.SinInOut) },
                { 0.5, 1, new Animation(v => outer.ScaleY = v, 1.1, 0.92, Easing.SinInOut) },
                { 0, 0.35, new Animation(v => inner.ScaleY = v, 1.0, 1.18, Easing.SinInOut) },
                { 0.35, 1, new Animation(v => inner.ScaleY = v, 1.18, 1.0, Easing.SinInOut) },
                { 0, 0.5, new Animation(v => glow.Opacity = v, 0.6, 1, Easing.SinInOut) },
                { 0.5, 1, new Animation(v => glow.Opacity = v, 1, 0.6, Easing.SinInOut) },
            };
            flicker.Commit(fire, "flicker", length: 1300, repeat: () => true);
        };
        fire.Unloaded += (_, _) => fire.AbortAnimation("flicker");
    }

    /// <summary>Bouton rond du cercle : icône dans une pastille, libellé dessous, compteur éventuel.</summary>
    public static View Node(string glyph, string label, Action onTap, string? badge = null, bool alert = false)
    {
        var disc = new Border
        {
            WidthRequest = 58,
            HeightRequest = 58,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 29 },
            Stroke = alert ? Theme.Red500 : Theme.Gold500,
            StrokeThickness = 1.5,
            BackgroundColor = Theme.Stone900,
            Content = Icon(glyph, 24, Theme.Gold400),
        };
        var top = new Grid { HorizontalOptions = LayoutOptions.Center, Children = { disc } };
        if (badge is not null)
        {
            var b = Badge(badge, alert ? Theme.Red600 : Theme.Gold700);
            b.HorizontalOptions = LayoutOptions.End;
            b.VerticalOptions = LayoutOptions.Start;
            b.TranslationX = 10;
            b.TranslationY = -4;
            top.Add(b);
        }
        var name = Caps(label, 9, Theme.Stone100);
        name.HorizontalTextAlignment = TextAlignment.Center;
        name.LineBreakMode = LineBreakMode.TailTruncation;
        return OnTap(new VerticalStackLayout { Spacing = 4, Children = { top, name } }, onTap);
    }

    /// <summary>Personnage assis autour du feu : visage, nom, et une ligne d'info (grade, PV...).</summary>
    public static View Person(View face, string name, View? info, Action onTap)
    {
        face.HorizontalOptions = LayoutOptions.Center;
        var label = Caps(name, 8, Theme.Stone100);
        label.HorizontalTextAlignment = TextAlignment.Center;
        label.LineBreakMode = LineBreakMode.TailTruncation;
        var stack = new VerticalStackLayout { Spacing = 2, Children = { face, label } };
        if (info is not null) stack.Add(info);
        return OnTap(stack, onTap);
    }
}
