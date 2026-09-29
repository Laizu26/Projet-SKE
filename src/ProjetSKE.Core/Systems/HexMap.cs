using ProjetSKE.Core.Data;

namespace ProjetSKE.Core.Systems;

/// <summary>Coordonnées hexagonales axiales (q, r), comme la carte de Service Impérial.</summary>
public readonly record struct Hex(int Q, int R)
{
    /// <summary>Les 6 voisins, dans l'ordre : est, nord-est, nord-ouest, ouest, sud-ouest, sud-est.</summary>
    public static readonly Hex[] Directions = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

    public Hex Neighbor(int direction) => new(Q + Directions[direction].Q, R + Directions[direction].R);

    public int DistanceTo(Hex other)
    {
        var dq = Q - other.Q;
        var dr = R - other.R;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }

    /// <summary>Toutes les cases d'un hexagone de rayon donné (rayon 2 = 19 cases).</summary>
    public static IEnumerable<Hex> Grid(int radius)
    {
        for (var q = -radius; q <= radius; q++)
            for (var r = Math.Max(-radius, -q - radius); r <= Math.Min(radius, -q + radius); r++)
                yield return new Hex(q, r);
    }

    /// <summary>Cases triées par anneau autour du centre (centre, puis anneau 1, anneau 2...).</summary>
    public static IEnumerable<Hex> Spiral(Hex center, int maxRadius)
    {
        yield return center;
        for (var radius = 1; radius <= maxRadius; radius++)
        {
            var hex = new Hex(center.Q + Directions[4].Q * radius, center.R + Directions[4].R * radius);
            for (var side = 0; side < 6; side++)
                for (var step = 0; step < radius; step++)
                {
                    yield return hex;
                    hex = hex.Neighbor(side);
                }
        }
    }
}

/// <summary>
/// Place les lieux du royaume sur la grille hexagonale : les positions fixées dans l'éditeur sont gardées,
/// les autres sont posées automatiquement à côté d'un lieu relié, sans chevauchement.
/// </summary>
public static class WorldLayout
{
    public static Dictionary<string, Hex> Compute(GameDatabase db)
    {
        var result = new Dictionary<string, Hex>();
        var used = new HashSet<Hex>();

        // Seuls les lieux du royaume sont sur la carte ; les sous-lieux sont à l'intérieur.
        var top = db.Content.Locations.Where(l => db.ParentOf(l) is null).ToList();
        foreach (var loc in top)
        {
            if (loc.HexQ is { } q && loc.HexR is { } r && used.Add(new Hex(q, r))) result[loc.Id] = new Hex(q, r);
        }

        // Parcours en largeur depuis le lieu de départ, puis depuis les lieux isolés restants.
        var roots = new List<string>();
        if (db.Locations.ContainsKey(db.Start.LocationId)) roots.Add(db.Start.LocationId);
        roots.AddRange(top.Select(l => l.Id));

        foreach (var root in roots)
        {
            if (!result.ContainsKey(root))
            {
                // Le premier lieu (le départ) va au centre, les lieux isolés sur la case libre la plus proche.
                foreach (var candidate in Hex.Spiral(new Hex(0, 0), 20))
                {
                    if (used.Add(candidate)) { result[root] = candidate; break; }
                }
            }
            var queue = new Queue<string>();
            queue.Enqueue(root);
            var visited = new HashSet<string> { root };
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!db.Locations.TryGetValue(id, out var loc)) continue;
                var index = 0;
                foreach (var next in loc.ConnectedIds.Where(n => db.Locations.TryGetValue(n, out var nl) && db.ParentOf(nl) is null))
                {
                    if (!result.ContainsKey(next)) Place(next, result[id], index);
                    index++;
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
        }
        return result;

        void Place(string id, Hex near, int preferred)
        {
            // Voisin libre en priorité (en tournant à partir d'une direction préférée), sinon la case libre la plus proche.
            for (var i = 0; i < 6; i++)
            {
                var candidate = near.Neighbor((preferred * 2 + i) % 6);
                if (used.Add(candidate)) { result[id] = candidate; return; }
            }
            foreach (var candidate in Hex.Spiral(near, 12))
            {
                if (used.Add(candidate)) { result[id] = candidate; return; }
            }
        }
    }
}
