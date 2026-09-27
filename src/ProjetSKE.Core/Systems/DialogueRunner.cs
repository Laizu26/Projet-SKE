using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Systems;

/// <summary>Fait avancer un dialogue réplique par réplique et applique ses actions (recrutement, objets, quêtes, combat...).</summary>
public sealed class DialogueRunner
{
    private readonly GameSession _session;
    private readonly Dictionary<string, DialogueNode> _nodes = [];
    private int _steps;

    public DialogueDef Dialogue { get; }
    public DialogueNode? Current { get; private set; }
    public bool IsFinished => Current is null;

    /// <summary>Monstres à combattre une fois le dialogue terminé (action "combat").</summary>
    public IReadOnlyList<string>? PendingBattle { get; private set; }

    internal DialogueRunner(GameSession session, DialogueDef dialogue)
    {
        _session = session;
        Dialogue = dialogue;
        foreach (var node in dialogue.Nodes) _nodes.TryAdd(node.Id, node);
        Enter(dialogue.StartId);
    }

    /// <summary>Choix proposés (ceux dont les conditions sont remplies).</summary>
    public IReadOnlyList<DialogueChoice> Choices =>
        Current?.Choices.Where(c => _session.CheckAll(c.Conditions)).ToList() ?? [];

    /// <summary>Passe à la réplique suivante (réplique sans choix disponible).</summary>
    public void Continue()
    {
        if (Current is null || Choices.Count > 0) return;
        Enter(Current.NextId);
    }

    public void Choose(int index)
    {
        var choices = Choices;
        if (index < 0 || index >= choices.Count) return;
        var choice = choices[index];
        Apply(choice.Actions);
        Enter(choice.NextId);
    }

    private void Enter(string? nodeId)
    {
        // Garde-fou contre une boucle infinie dans un dialogue mal construit.
        if (++_steps > 500) nodeId = null;
        Current = !string.IsNullOrEmpty(nodeId) && _nodes.TryGetValue(nodeId, out var node) ? node : null;
        if (Current is not null) Apply(Current.Actions);
    }

    private void Apply(IEnumerable<GameAction> actions)
    {
        foreach (var action in actions)
        {
            if (_session.Execute(action) is { } battle) PendingBattle = battle;
        }
    }
}
