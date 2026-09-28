using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Ui;

/// <summary>Une fissure : une ligne brisée, qui apparaît à un palier donné.</summary>
internal sealed record CrackLine(PointF[] Points, int Stage, float Width);

/// <summary>
/// Dessin des fissures. Elles sont tirées au hasard une fois (graine fixe : toujours les mêmes pour une taille d'écran)
/// et révélées palier par palier : d'abord quelques fêlures dans les coins, puis sur les bords, et au dernier palier
/// un impact au milieu de l'écran, en toile d'araignée.
/// </summary>
internal static class CrackGenerator
{
    public static List<CrackLine> Generate(float w, float h, int stages, int seed = 1337)
    {
        var lines = new List<CrackLine>();
        if (stages <= 0 || w <= 0 || h <= 0) return lines;
        var rng = new Random(seed);
        var size = Math.Min(w, h);
        var center = new PointF(w / 2, h / 2);

        for (var stage = 1; stage <= stages; stage++)
        {
            var last = stage == stages && stages >= 2;
            if (last)
            {
                // Impact près du centre : fissures en étoile, reliées par des anneaux.
                var impact = new PointF(w * (0.42f + 0.16f * (float)rng.NextDouble()), h * (0.4f + 0.2f * (float)rng.NextDouble()));
                var spokes = 11;
                var ends = new List<PointF[]>();
                for (var i = 0; i < spokes; i++)
                {
                    var angle = (float)(2 * Math.PI * i / spokes + rng.NextDouble() * 0.4);
                    var path = Walk(rng, impact, angle, steps: 9 + rng.Next(5), step: size * 0.07f, jitter: 0.35f);
                    ends.Add(path);
                    lines.Add(new CrackLine(path, stage, 2.2f));
                    Branches(rng, lines, path, stage, size, 1.3f);
                }
                foreach (var ring in new[] { 1, 3, 5 })
                {
                    var points = ends.Where(p => p.Length > ring).Select(p => p[ring]).ToList();
                    for (var i = 0; i < points.Count; i++)
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        lines.Add(new CrackLine([points[i], Mid(points[i], points[(i + 1) % points.Count], rng, size * 0.02f), points[(i + 1) % points.Count]], stage, 1.2f));
                    }
                }
                continue;
            }

            // Fêlures qui partent des coins (premier palier) puis des bords, vers l'intérieur.
            var count = 2 + stage * 2;
            for (var i = 0; i < count; i++)
            {
                PointF origin;
                if (stage == 1)
                {
                    origin = (i % 4) switch
                    {
                        0 => new PointF(w, h),
                        1 => new PointF(0, 0),
                        2 => new PointF(w, 0),
                        _ => new PointF(0, h),
                    };
                }
                else
                {
                    var t = (float)rng.NextDouble();
                    origin = rng.Next(4) switch
                    {
                        0 => new PointF(t * w, 0),
                        1 => new PointF(t * w, h),
                        2 => new PointF(0, t * h),
                        _ => new PointF(w, t * h),
                    };
                }
                var toward = MathF.Atan2(center.Y - origin.Y, center.X - origin.X) + (float)(rng.NextDouble() - 0.5) * 1.2f;
                var steps = 4 + stage * 3 + rng.Next(3);
                var path = Walk(rng, origin, toward, steps, size * 0.05f, 0.45f);
                lines.Add(new CrackLine(path, stage, 1.6f + stage * 0.2f));
                Branches(rng, lines, path, stage, size, 1f);
            }
        }
        return lines;
    }

    private static PointF[] Walk(Random rng, PointF start, float angle, int steps, float step, float jitter)
    {
        var points = new PointF[steps + 1];
        points[0] = start;
        var p = start;
        for (var i = 1; i <= steps; i++)
        {
            angle += (float)(rng.NextDouble() - 0.5) * 2 * jitter;
            var len = step * (0.6f + (float)rng.NextDouble() * 0.8f);
            p = new PointF(p.X + MathF.Cos(angle) * len, p.Y + MathF.Sin(angle) * len);
            points[i] = p;
        }
        return points;
    }

    private static void Branches(Random rng, List<CrackLine> lines, PointF[] path, int stage, float size, float width)
    {
        for (var i = 2; i < path.Length - 1; i++)
        {
            if (rng.NextDouble() > 0.28) continue;
            var dir = MathF.Atan2(path[i].Y - path[i - 1].Y, path[i].X - path[i - 1].X) + (rng.NextDouble() < 0.5 ? -1 : 1) * (0.5f + (float)rng.NextDouble() * 0.6f);
            lines.Add(new CrackLine(Walk(rng, path[i], dir, 2 + rng.Next(3), size * 0.035f, 0.5f), stage, width * 0.8f));
        }
    }

    private static PointF Mid(PointF a, PointF b, Random rng, float wobble) =>
        new((a.X + b.X) / 2 + (float)(rng.NextDouble() - 0.5) * wobble, (a.Y + b.Y) / 2 + (float)(rng.NextDouble() - 0.5) * wobble);

    public static void Draw(ICanvas canvas, IEnumerable<CrackLine> lines, int level, float grow, float intensity)
    {
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        var alpha = Math.Clamp(intensity, 0.2f, 2f);
        foreach (var line in lines)
        {
            if (line.Stage > level) continue;
            var count = line.Stage == level ? Math.Max(2, (int)Math.Ceiling(line.Points.Length * grow)) : line.Points.Length;
            count = Math.Min(count, line.Points.Length);
            if (count < 2) continue;
            var path = new PathF(line.Points[0]);
            for (var i = 1; i < count; i++) path.LineTo(line.Points[i]);
            // Ombre (verre épais), puis éclat clair.
            canvas.StrokeColor = Colors.Black.WithAlpha(Math.Min(1, 0.35f * alpha));
            canvas.StrokeSize = line.Width * alpha + 1.5f;
            canvas.DrawPath(path);
            canvas.StrokeColor = Colors.White.WithAlpha(Math.Min(1, 0.7f * alpha));
            canvas.StrokeSize = Math.Max(0.6f, line.Width * alpha * 0.55f);
            canvas.DrawPath(path);
        }
    }
}

/// <summary>Couche de fissures posée sur tout l'écran de jeu (ne bloque aucun toucher).</summary>
public sealed class CrackOverlay : GraphicsView
{
    private sealed class Painter : IDrawable
    {
        public int Level;
        public int Stages;
        public float Grow = 1;
        public float Intensity = 1;
        private List<CrackLine> _lines = [];
        private (float W, float H, int Stages) _key;

        public void Draw(ICanvas canvas, RectF rect)
        {
            if (Level <= 0 || Stages <= 0) return;
            if (_key != (rect.Width, rect.Height, Stages))
            {
                _key = (rect.Width, rect.Height, Stages);
                _lines = CrackGenerator.Generate(rect.Width, rect.Height, Stages);
            }
            // Bords assombris, de plus en plus à chaque palier.
            var dark = Math.Clamp(0.08f * Level * Intensity, 0, 0.45f);
            canvas.SetFillPaint(new RadialGradientPaint
            {
                GradientStops =
                [
                    new PaintGradientStop(0.55f, Colors.Transparent),
                    new PaintGradientStop(1f, Color.FromRgba(40, 0, 0, (int)(dark * 255))),
                ],
            }, rect);
            canvas.FillRectangle(rect);
            CrackGenerator.Draw(canvas, _lines, Level, Grow, Intensity);
        }
    }

    private readonly Painter _drawable = new();

    public CrackOverlay()
    {
        Drawable = _drawable;
        InputTransparent = true;
        BackgroundColor = Colors.Transparent;
    }

    public int Level => _drawable.Level;

    /// <summary>Change le palier ; un nouveau palier fait « courir » ses fissures.</summary>
    public void SetLevel(int level, CrackSettings settings, bool animate)
    {
        _drawable.Stages = settings.MaxLevel;
        _drawable.Intensity = (float)Math.Clamp(settings.Intensity, 0.2, 2);
        var grew = level > _drawable.Level;
        _drawable.Level = level;
        IsVisible = level > 0;
        this.AbortAnimation("crack");
        if (grew && animate)
        {
            this.Animate("crack", v => { _drawable.Grow = (float)v; Invalidate(); }, 0, 1, length: 450, easing: Easing.CubicOut);
        }
        else
        {
            _drawable.Grow = 1;
            Invalidate();
        }
    }
}

/// <summary>
/// L'écran éclate : l'image de l'écran (capturée juste avant) est découpée en éclats de verre
/// qui tombent en tournant, sur fond noir.
/// </summary>
public sealed class ShatterView : GraphicsView
{
    private sealed record Shard(PointF[] Points, PointF Center, float Vx, float Spin, float Delay);

    private sealed class Painter(Microsoft.Maui.Graphics.IImage? image, int stages) : IDrawable
    {
        public float Time;
        private List<Shard>? _shards;
        private List<CrackLine> _cracks = [];
        private PointF _impact;

        private const float Freeze = 0.35f;

        public void Draw(ICanvas canvas, RectF rect)
        {
            float w = rect.Width, h = rect.Height;
            canvas.FillColor = Colors.Black;
            canvas.FillRectangle(rect);
            if (_shards is null) Build(w, h);

            foreach (var shard in _shards!)
            {
                var t = Math.Max(0, Time - Freeze - shard.Delay);
                var dy = 0.5f * 2.4f * h * t * t;
                var dx = shard.Vx * w * t;
                var angle = shard.Spin * t;
                if (dy > h * 1.4f) continue;
                var path = new PathF(shard.Points[0]);
                for (var i = 1; i < shard.Points.Length; i++) path.LineTo(shard.Points[i]);
                path.Close();

                canvas.SaveState();
                canvas.Translate(shard.Center.X + dx, shard.Center.Y + dy);
                canvas.Rotate(angle);
                canvas.Translate(-shard.Center.X, -shard.Center.Y);
                canvas.Alpha = Math.Clamp(1 - dy / (h * 1.2f), 0, 1);
                canvas.SaveState();
                canvas.ClipPath(path);
                if (image is not null) canvas.DrawImage(image, 0, 0, w, h);
                else
                {
                    canvas.FillColor = Color.FromArgb("#292524");
                    canvas.FillRectangle(0, 0, w, h);
                }
                canvas.RestoreState();
                canvas.StrokeColor = Colors.White.WithAlpha(t > 0 ? 0.55f : 0.8f);
                canvas.StrokeSize = 1.2f;
                canvas.DrawPath(path);
                canvas.RestoreState();
            }
            // Juste avant l'éclatement : toutes les fissures, bien visibles.
            if (Time < Freeze) CrackGenerator.Draw(canvas, _cracks, stages, 1, 1.4f);
        }

        private void Build(float w, float h)
        {
            var rng = new Random(7);
            _cracks = CrackGenerator.Generate(w, h, Math.Max(1, stages));
            _impact = new PointF(w / 2, h / 2);
            const int cols = 4, rows = 7;
            var grid = new PointF[cols + 1, rows + 1];
            for (var x = 0; x <= cols; x++)
                for (var y = 0; y <= rows; y++)
                {
                    var edgeX = x == 0 || x == cols;
                    var edgeY = y == 0 || y == rows;
                    var jx = edgeX ? 0 : (float)(rng.NextDouble() - 0.5) * w / cols * 0.7f;
                    var jy = edgeY ? 0 : (float)(rng.NextDouble() - 0.5) * h / rows * 0.7f;
                    grid[x, y] = new PointF(w * x / cols + jx, h * y / rows + jy);
                }
            var maxDist = MathF.Sqrt(w * w + h * h) / 2;
            _shards = [];
            for (var x = 0; x < cols; x++)
                for (var y = 0; y < rows; y++)
                {
                    var a = grid[x, y];
                    var b = grid[x + 1, y];
                    var c = grid[x + 1, y + 1];
                    var d = grid[x, y + 1];
                    // Chaque case en deux éclats, coupés dans un sens ou dans l'autre.
                    var tris = rng.Next(2) == 0 ? new[] { new[] { a, b, c }, new[] { a, c, d } } : new[] { new[] { a, b, d }, new[] { b, c, d } };
                    foreach (var tri in tris)
                    {
                        var center = new PointF((tri[0].X + tri[1].X + tri[2].X) / 3, (tri[0].Y + tri[1].Y + tri[2].Y) / 3);
                        var dist = MathF.Sqrt((center.X - _impact.X) * (center.X - _impact.X) + (center.Y - _impact.Y) * (center.Y - _impact.Y));
                        _shards.Add(new Shard(tri, center,
                            Vx: (center.X - _impact.X) / w * 0.8f + (float)(rng.NextDouble() - 0.5) * 0.2f,
                            Spin: (float)(rng.NextDouble() - 0.5) * 260,
                            Delay: dist / maxDist * 0.45f + (float)rng.NextDouble() * 0.12f));
                    }
                }
        }
    }

    private readonly Painter _drawable;

    public ShatterView(Microsoft.Maui.Graphics.IImage? image, int stages)
    {
        _drawable = new Painter(image, stages);
        Drawable = _drawable;
        BackgroundColor = Colors.Black;
    }

    /// <summary>Joue l'éclatement (environ deux secondes).</summary>
    public Task PlayAsync()
    {
        var done = new TaskCompletionSource();
        const uint length = 2200;
        this.Animate("shatter", v => { _drawable.Time = (float)(v * length / 1000.0); Invalidate(); }, 0, 1,
            length: length, easing: Easing.Linear, finished: (_, _) => done.TrySetResult());
        return done.Task;
    }
}
