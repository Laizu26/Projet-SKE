using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.App.Ui;

/// <summary>Une tuile de la carte hexagonale.</summary>
public sealed record HexTileSpec(
    Hex Hex,
    Color Fill,
    Color Stroke,
    string? Icon = null,
    string? Label = null,
    bool IsCurrent = false,
    bool IsSelected = false,
    bool Dashed = false,
    Action? OnTap = null,
    string? Badge = null,
    double Opacity = 1);

/// <summary>Une route entre deux tuiles.</summary>
public sealed record HexRoad(Hex From, Hex To, bool Highlight);

/// <summary>Un pion posé sur une tuile (ex : l'équipe).</summary>
public sealed record HexToken(Hex Hex, Color Color, string Initial);

/// <summary>
/// Carte en hexagones « pointe en haut », reprise de la carte de Service Impérial :
/// tuiles colorées avec icône et nom, contour ambré qui pulse sur la position actuelle,
/// routes en pointillés, fond parchemin. La taille s'adapte à la largeur de l'écran.
/// </summary>
public sealed class HexMapView : ContentView
{
    private const double Sqrt3 = 1.7320508075688772;

    private readonly IReadOnlyList<HexTileSpec> _tiles;
    private readonly IReadOnlyList<HexRoad> _roads;
    private readonly IReadOnlyList<HexToken> _tokens;
    private double _size;
    private AbsoluteLayout? _canvas;

    /// <summary>Dernière carte affichée (vérification de la mise en page par le test automatique).</summary>
    public static HexMapView? Last { get; private set; }

    public HexMapView(IReadOnlyList<HexTileSpec> tiles, IReadOnlyList<HexRoad>? roads = null, IReadOnlyList<HexToken>? tokens = null)
    {
        _tiles = tiles;
        _roads = roads ?? [];
        _tokens = tokens ?? [];
        Last = this;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Start;
        // Taille des cases choisie tout de suite d'après la largeur de la fenêtre (colonne du jeu) : la carte
        // a sa bonne taille dès le premier affichage. Sur Android, la reconstruire pendant le calcul de la mise
        // en page la laissait à l'ancienne taille, décalée dans un grand cadre vide.
        _size = SizeFor(EstimatedWidth());
        Build(_size);
        SizeChanged += (_, _) =>
        {
            if (Width <= 0) return;
            var size = SizeFor(Width);
            if (Math.Abs(size - _size) < 0.5) return;
            _size = size;
            // Hors du calcul de mise en page en cours, puis on redemande la mesure.
            Dispatcher.Dispatch(() =>
            {
                Build(size);
                InvalidateMeasure();
            });
        };
    }

    /// <summary>Largeur probable de la carte avant la première mesure : la fenêtre (au plus la colonne du jeu), moins les marges.</summary>
    private static double EstimatedWidth()
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Width ?? 0;
        if (window <= 0)
        {
            var display = DeviceDisplay.Current.MainDisplayInfo;
            window = display.Density > 0 ? display.Width / display.Density : 360;
        }
        return Math.Min(window, Responsive.GameWidth) - 56;
    }

    /// <summary>Taille des cases pour que toute la carte tienne dans cette largeur (les marges fixes comptées à part).</summary>
    private double SizeFor(double width)
    {
        if (_tiles.Count == 0) return 34;
        var xs = _tiles.Select(t => Center(t.Hex, 1).X).ToList();
        var span = xs.Max() - xs.Min() + Sqrt3; // largeur de la carte pour des cases de taille 1, sans les marges
        return Math.Clamp((width - 10) / span, 16, 48);
    }

    /// <summary>
    /// Problème de mise en page visible (carte qui déborde, décalée, ou perdue dans un cadre trop grand), sinon null.
    /// Sert au test automatique, sur téléphone et sur PC.
    /// </summary>
    public string? LayoutProblem()
    {
        if (_canvas is not { } c || Width <= 0 || c.Width <= 0) return "carte pas encore affichée";
        if (c.Width > Width + 2) return $"la carte déborde ({c.Width:0} > {Width:0})";
        if (c.X < -1 || c.X + c.Width > Width + 2) return $"carte décalée (x = {c.X:0}, largeur {c.Width:0} sur {Width:0})";
        if (Height > c.Height + 30) return $"cadre trop haut ({Height:0} pour une carte de {c.Height:0})";
        return null;
    }

    private static (double X, double Y) Center(Hex h, double size) =>
        (size * Sqrt3 * (h.Q + h.R / 2.0), size * 1.5 * h.R);

    private (double MinX, double MinY, double MaxX, double MaxY) MapBounds(double size)
    {
        if (_tiles.Count == 0) return (0, 0, 1, 1);
        var centers = _tiles.Select(t => Center(t.Hex, size)).ToList();
        var padX = size * Sqrt3 / 2 + 4;
        var padY = size + 4;
        return (centers.Min(c => c.X) - padX, centers.Min(c => c.Y) - padY, centers.Max(c => c.X) + padX, centers.Max(c => c.Y) + padY);
    }

    private static PointCollection HexPoints(double w, double h, double inset)
    {
        return
        [
            new Point(w / 2, inset),
            new Point(w - inset, h / 4 + inset / 2),
            new Point(w - inset, 3 * h / 4 - inset / 2),
            new Point(w / 2, h - inset),
            new Point(inset, 3 * h / 4 - inset / 2),
            new Point(inset, h / 4 + inset / 2),
        ];
    }

    private void Build(double size)
    {
        var (minX, minY, maxX, maxY) = MapBounds(size);
        var canvas = new AbsoluteLayout
        {
            WidthRequest = maxX - minX,
            HeightRequest = maxY - minY,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
        };
        _canvas = canvas;
        var full = new Rect(0, 0, maxX - minX, maxY - minY);

        // Routes sous les tuiles.
        foreach (var road in _roads)
        {
            var (x1, y1) = Center(road.From, size);
            var (x2, y2) = Center(road.To, size);
            var line = new Line
            {
                X1 = x1 - minX, Y1 = y1 - minY, X2 = x2 - minX, Y2 = y2 - minY,
                Stroke = road.Highlight ? Theme.Gold600 : Color.FromArgb("#A8927A"),
                StrokeThickness = road.Highlight ? 4 : 3,
                StrokeLineCap = PenLineCap.Round,
                InputTransparent = true,
            };
            if (!road.Highlight) line.StrokeDashArray = new DoubleCollection { 2, 1.5 };
            AbsoluteLayout.SetLayoutBounds(line, full);
            canvas.Add(line);
        }

        var w = Sqrt3 * size;
        var h = 2 * size;
        foreach (var tile in _tiles)
        {
            var (cx, cy) = Center(tile.Hex, size);
            var cell = new Grid { WidthRequest = w, HeightRequest = h, Opacity = tile.Opacity };

            var hexShape = new Polygon
            {
                Points = HexPoints(w, h, 1.5),
                Fill = tile.Fill,
                Stroke = tile.IsSelected ? Theme.Stone900 : tile.Stroke,
                StrokeThickness = tile.IsSelected ? 2.6 : 1.2,
            };
            if (tile.Dashed) hexShape.StrokeDashArray = new DoubleCollection { 3, 2 };
            cell.Add(hexShape);

            if (tile.IsCurrent)
            {
                var outline = new Polygon
                {
                    Points = HexPoints(w, h, 0.5),
                    Stroke = Color.FromArgb("#D97706"),
                    StrokeThickness = 3,
                    InputTransparent = true,
                };
                cell.Add(outline);
                Pulse(outline);
            }

            if (tile.Icon is not null || tile.Label is not null)
            {
                var content = new VerticalStackLayout
                {
                    Spacing = 1,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    InputTransparent = true,
                    MaximumWidthRequest = w - 8,
                };
                if (tile.Icon is not null)
                    content.Add(new Label { Text = tile.Icon, FontFamily = Ico.Font, FontSize = size * 0.42, TextColor = Theme.InkOn(tile.Fill), Opacity = 0.85, HorizontalTextAlignment = TextAlignment.Center });
                if (tile.Label is not null)
                {
                    content.Add(new Label
                    {
                        Text = tile.Label,
                        FontSize = Math.Max(7, size * 0.21),
                        FontAttributes = FontAttributes.Bold,
                        TextColor = tile.IsCurrent ? (Theme.IsLight(tile.Fill) ? Color.FromArgb("#B45309") : Theme.Amber500) : Theme.InkOn(tile.Fill),
                        HorizontalTextAlignment = TextAlignment.Center,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines = 2,
                    });
                }
                cell.Add(content);
            }

            if (tile.Badge is not null)
            {
                cell.Add(new Border
                {
                    WidthRequest = 18,
                    HeightRequest = 18,
                    StrokeShape = new Ellipse(),
                    StrokeThickness = 0,
                    BackgroundColor = Night.Stone900,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, size * 0.25, 2, 0),
                    InputTransparent = true,
                    Content = new Label { Text = tile.Badge, FontFamily = Ico.Font, FontSize = 10, TextColor = Theme.Gold500, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center },
                });
            }

            if (tile.OnTap is { } tap) UiKit.OnTap(cell, tap);
            AbsoluteLayout.SetLayoutBounds(cell, new Rect(cx - minX - w / 2, cy - minY - h / 2, w, h));
            canvas.Add(cell);
        }

        // Pions (l'équipe) par-dessus.
        foreach (var token in _tokens)
        {
            var (cx, cy) = Center(token.Hex, size);
            var d = Math.Max(16, size * 0.55);
            var pawn = new Border
            {
                WidthRequest = d,
                HeightRequest = d,
                StrokeShape = new Ellipse(),
                Stroke = Colors.White,
                StrokeThickness = 2,
                BackgroundColor = token.Color,
                InputTransparent = true,
                Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 2), Radius = 4, Opacity = 0.5f },
                Content = new Label { Text = token.Initial, FontSize = d * 0.5, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center },
            };
            AbsoluteLayout.SetLayoutBounds(pawn, new Rect(cx - minX - d / 2, cy - minY + size * 0.15, d, d));
            canvas.Add(pawn);
        }

        Content = canvas;
    }

    /// <summary>Contour qui « respire » sur la position actuelle.</summary>
    private static void Pulse(VisualElement element)
    {
        var animation = new Animation
        {
            { 0, 0.5, new Animation(v => element.Opacity = v, 0.95, 0.3) },
            { 0.5, 1, new Animation(v => element.Opacity = v, 0.3, 0.95) },
        };
        element.Loaded += (_, _) => animation.Commit(element, "pulse", 16, 2200, Easing.SinInOut, repeat: () => element.IsLoaded);
    }
}
