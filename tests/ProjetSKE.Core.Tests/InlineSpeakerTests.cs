using ProjetSKE.Core.Data;

namespace ProjetSKE.Core.Tests;

/// <summary>Bulles écrites dans le fil du texte : « Nom : » à la française, en milieu de ligne, narration « - ».</summary>
public class InlineSpeakerTests
{
    private static readonly string[] Names = ["Julius Callius", "Julius", "Narration"];

    [Fact]
    public void KnownNameAfterSentence_StartsABubble_FrenchColon()
    {
        var text = "Une petite foule se rassemble, puis... il prend la parole.Julius : Mes frères et sœurs, cet incident est regrettable.- son armure luisante ne suffisait pas.";
        var segments = DialogueScript.Segments("", text, Names);
        Assert.Equal(3, segments.Count);
        Assert.Equal(("", "Une petite foule se rassemble, puis... il prend la parole."), segments[0]);
        Assert.Equal("Julius", segments[1].Speaker);
        Assert.StartsWith("Mes frères et sœurs", segments[1].Text);
        Assert.Equal(("", "son armure luisante ne suffisait pas."), segments[2]);
    }

    [Fact]
    public void KnownNameAtLineStart_WithSpaceBeforeColon()
    {
        var segments = DialogueScript.Segments("", "- La porte s'ouvre.\nJulius Callius : Entrez.", Names);
        Assert.Equal(2, segments.Count);
        Assert.Equal(("Julius Callius", "Entrez."), segments[1]);
    }

    [Fact]
    public void UnknownWordsWithColon_StayInTheText()
    {
        var segments = DialogueScript.Segments("Garde", "Bonjour. Note : rien à signaler.", Names);
        Assert.Single(segments);
        Assert.Equal("Bonjour. Note : rien à signaler.", segments[0].Text);
    }
}
