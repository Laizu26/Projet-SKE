using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Systems;

/// <summary>Fait avancer un dialogue nœud par nœud et applique ses actions (recrutement, objets, combat...).</summary>
public sealed class DialogueRunner
{
    private readonly GameSession _session;
    private readonly Dictionary<string, DialogueNode> _nodes;

    public DialogueDef Dialogue { get; }
    public DialogueNode? Current { get; private set; }
    public bool IsFinished => Current is null;

    /// <summary>Monstres à combattre une fois le dialogue terminé (action StartBattle).</summary>
    public IReadOnlyList<string>? PendingBattle { get; private set; }

    /// <summary>Messages système à afficher (ex : "Lyra rejoint l'équipe !").</summary>
    public List<string> Notifications { get; } = [];

    internal DialogueRunner(GameSession session, DialogueDef dialogue)
    {
        _session = session;
        Dialogue = dialogue;
        _nodes = dialogue.Nodes.ToDictionary(n => n.Id);
        Enter(dialogue.StartId);
    }

    public IReadOnlyList<DialogueChoice> Choices => Current?.Choices ?? [];

    /// <summary>Passe au nœud suivant (nœud sans choix).</summary>
    public void Continue()
    {
        if (Current is null || Current.Choices.Count > 0) return;
        Enter(Current.NextId);
    }

    public void Choose(int index)
    {
        if (Current is null || index < 0 || index >= Current.Choices.Count) return;
        var choice = Current.Choices[index];
        Apply(choice.Actions);
        Enter(choice.NextId);
    }

    private void Enter(string? nodeId)
    {
        Current = nodeId is not null && _nodes.TryGetValue(nodeId, out var node) ? node : null;
        if (Current is not null) Apply(Current.Actions);
    }

    private void Apply(IEnumerable<DialogueAction> actions)
    {
        foreach (var action in actions)
        {
            switch (action.Type)
            {
                case DialogueActionType.SetFlag:
                    _session.SetFlag(action.Arg);
                    break;
                case DialogueActionType.Recruit:
                    if (_session.Recruit(action.Arg))
                    {
                        var c = _session.State.Party[^1];
                        Notifications.Add($"{_session.Db.Characters[action.Arg].Name} rejoint l'équipe{(c.IsActive ? "" : " (réserve)")} !");
                    }
                    break;
                case DialogueActionType.GiveItem:
                    if (_session.AddItem(action.Arg, action.Amount))
                        Notifications.Add($"Obtenu : {_session.Db.Items[action.Arg].Name} x{action.Amount}");
                    break;
                case DialogueActionType.GiveGold:
                    _session.State.Gold += action.Amount;
                    Notifications.Add($"Obtenu : {action.Amount} or");
                    break;
                case DialogueActionType.StartBattle:
                    PendingBattle = action.Arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
            }
        }
    }
}
