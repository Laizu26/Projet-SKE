namespace ProjetSKE.Core.Systems;

/// <summary>
/// Carnet du joueur découpé en pages de taille fixe : le texte (GameState.Journal) garde ses pages séparées par
/// un saut de page (\f). Quand une page est pleine, la suite passe sur la page suivante.
/// </summary>
public static class Notebook
{
    public const char PageBreak = '\f';

    public static List<string> Pages(string journal)
    {
        var pages = (journal ?? "").Split(PageBreak).ToList();
        return pages.Count == 0 ? [""] : pages;
    }

    /// <summary>Recolle les pages ; les pages vides à la fin sont retirées (sauf la première).</summary>
    public static string Join(IReadOnlyList<string> pages)
    {
        var list = pages.ToList();
        while (list.Count > 1 && list[^1].Length == 0) list.RemoveAt(list.Count - 1);
        return string.Join(PageBreak, list);
    }

    /// <summary>Nombre de lignes affichées pour un texte (retour à la ligne automatique, mot par mot).</summary>
    public static int LineCount(string text, int charsPerLine)
    {
        charsPerLine = Math.Max(4, charsPerLine);
        var lines = 0;
        foreach (var paragraph in text.Split('\n'))
        {
            var line = 0;
            lines++;
            foreach (var word in paragraph.Split(' '))
            {
                var length = word.Length;
                if (line == 0) line = length;
                else if (line + 1 + length <= charsPerLine) line += 1 + length;
                else { lines++; line = length; }
                // Mot plus long qu'une ligne : il est coupé.
                while (line > charsPerLine) { lines++; line -= charsPerLine; }
            }
        }
        return lines;
    }

    /// <summary>
    /// Coupe un texte pour qu'il tienne dans une page (<paramref name="maxLines"/> lignes) : ce qui tient, et le reste
    /// (vide si tout tient). La coupe se fait entre deux mots (ou à un retour à la ligne).
    /// </summary>
    public static (string Fit, string Overflow) Split(string text, int charsPerLine, int maxLines)
    {
        maxLines = Math.Max(1, maxLines);
        if (LineCount(text, charsPerLine) <= maxLines) return (text, "");
        // Recherche du plus long début qui tient.
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (LineCount(text[..mid], charsPerLine) <= maxLines) lo = mid; else hi = mid - 1;
        }
        var cut = lo;
        // Recule jusqu'à une espace ou un retour à la ligne (sans couper un mot), si possible.
        var soft = text.LastIndexOfAny([' ', '\n'], Math.Max(0, cut - 1));
        if (soft > 0 && cut < text.Length && text[cut] is not (' ' or '\n')) cut = soft;
        var fit = text[..cut].TrimEnd(' ');
        var rest = text[cut..].TrimStart(' ', '\n');
        return (fit, rest);
    }
}
