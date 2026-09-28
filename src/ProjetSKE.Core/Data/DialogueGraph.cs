using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>Un lien d'une réplique vers une autre (suite, sinon, aiguillage, choix).</summary>
public sealed record DialogueLink(DialogueNode From, string Kind, string Target, Action<string?> Set);

/// <summary>
/// Liens entre répliques, pour l'éditeur : liens cassés, répliques que plus rien n'atteint,
/// liens qui mènent à une réplique (avant de la supprimer).
/// </summary>
public static class DialogueGraph
{
    /// <summary>Tous les liens sortants d'une réplique (sauf « fin »).</summary>
    public static IEnumerable<DialogueLink> Links(DialogueNode n)
    {
        if (n.NextId is { Length: > 0 } next) yield return new(n, "suite", next, v => n.NextId = v);
        if (n.ElseId is { Length: > 0 } otherwise && otherwise != "fin") yield return new(n, "sinon", otherwise, v => n.ElseId = v);
        foreach (var b in n.Branches)
            if (b.NextId is { Length: > 0 } t) yield return new(n, "aiguillage", t, v => b.NextId = v);
        foreach (var c in n.Choices)
            if (c.NextId is { Length: > 0 } t) yield return new(n, $"choix « {c.Text} »", t, v => c.NextId = v);
    }

    /// <summary>La réplique visée par un lien (null si introuvable).</summary>
    public static DialogueNode? Resolve(GameContent content, DialogueDef from, string target)
    {
        var (dialogueId, label) = GameDatabase.SplitTarget(target);
        var dialogue = dialogueId is null ? from : content.Dialogues.FirstOrDefault(d => d.Id == dialogueId);
        if (dialogue is null) return null;
        return label.Length == 0 ? dialogue.Nodes.FirstOrDefault() : dialogue.Nodes.FirstOrDefault(x => x.Id == label);
    }

    /// <summary>Liens de ce dialogue qui ne mènent nulle part.</summary>
    public static List<DialogueLink> Broken(GameContent content, DialogueDef dialogue) =>
        dialogue.Nodes.SelectMany(Links).Where(l => Resolve(content, dialogue, l.Target) is null).ToList();

    /// <summary>Liens (de tous les dialogues) qui mènent à cette réplique.</summary>
    public static List<DialogueLink> Incoming(GameContent content, DialogueDef dialogue, DialogueNode node) =>
        content.Dialogues.SelectMany(d => d.Nodes.SelectMany(Links).Where(l => ReferenceEquals(Resolve(content, d, l.Target), node))).ToList();

    /// <summary>Répliques que rien n'atteint (ni la première, ni une suite, un choix, un aiguillage...).</summary>
    public static List<DialogueNode> Unreachable(GameContent content, DialogueDef dialogue) =>
        dialogue.Nodes.Skip(1).Where(n => Incoming(content, dialogue, n).Count == 0).ToList();

    /// <summary>Supprime une réplique ; les liens qui y menaient mènent désormais à la fin.</summary>
    public static int Remove(GameContent content, DialogueDef dialogue, DialogueNode node)
    {
        var incoming = Incoming(content, dialogue, node);
        foreach (var link in incoming) link.Set(null);
        dialogue.Nodes.Remove(node);
        return incoming.Count;
    }
}
