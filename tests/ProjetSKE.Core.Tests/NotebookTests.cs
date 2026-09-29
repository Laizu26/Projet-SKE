using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Carnet en pages : la page pleine passe la suite à la page suivante.</summary>
public class NotebookTests
{
    [Fact]
    public void LineCount_WrapsWordByWord()
    {
        Assert.Equal(1, Notebook.LineCount("", 10));
        Assert.Equal(2, Notebook.LineCount("aaaa bbbb cccc", 10)); // « aaaa bbbb » puis « cccc »
        Assert.Equal(3, Notebook.LineCount("a\nb\nc", 10));
    }

    [Fact]
    public void Split_KeepsWholeWords_AndGivesTheRest()
    {
        var (fit, rest) = Notebook.Split("un deux trois quatre cinq six", 10, 2);
        Assert.True(Notebook.LineCount(fit, 10) <= 2);
        Assert.Equal("un deux trois quatre cinq six", (fit + " " + rest).Trim());
        Assert.DoesNotContain(fit.Split(' '), w => w.Length > 0 && !"un deux trois quatre cinq six".Split(' ').Contains(w));
    }

    [Fact]
    public void Split_EverythingFits()
    {
        Assert.Equal(("court", ""), Notebook.Split("court", 20, 3));
    }

    [Fact]
    public void Pages_RoundTrip_AndDropEmptyTrailingPages()
    {
        var pages = Notebook.Pages("page 1\fpage 2");
        Assert.Equal(["page 1", "page 2"], pages);
        pages.Add("");
        Assert.Equal("page 1\fpage 2", Notebook.Join(pages));
        Assert.Equal([""], Notebook.Pages(""));
    }
}
