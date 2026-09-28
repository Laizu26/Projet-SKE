using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Systems;

/// <summary>Un choix tel que présenté au joueur : disponible, ou grisé avec la raison.</summary>
public sealed record ChoiceOption(DialogueChoice Choice, string Text, bool Enabled, string LockedText);

/// <summary>
/// Fait avancer un dialogue réplique par réplique et applique ses actions (recrutement, objets, quêtes, combat...).
/// Les répliques peuvent sauter dans un autre dialogue (« dialogue:etiquette ») : les histoires se croisent.
/// Le texte affiché dépend de la situation (variantes, balises %pj%, %karma%...).
/// </summary>
public sealed class DialogueRunner
{
    private readonly GameSession _session;
    private int _steps;

    /// <summary>Dialogue en cours (change quand une réplique mène dans un autre dialogue).</summary>
    public DialogueDef Dialogue { get; private set; }
    public DialogueNode? Current { get; private set; }
    public bool IsFinished => Current is null;

    /// <summary>Monstres à combattre une fois le dialogue terminé (action "combat").</summary>
    public IReadOnlyList<string>? PendingBattle { get; private set; }

    internal DialogueRunner(GameSession session, DialogueDef dialogue)
    {
        _session = session;
        Dialogue = dialogue;
        Enter(dialogue.StartId);
    }

    /// <summary>Version de la réplique jouée (première variante dont les conditions passent).</summary>
    private (string Speaker, string Text) Line
    {
        get
        {
            if (Current is null) return ("", "");
            var variant = Current.Variants.FirstOrDefault(v => _session.CheckAll(v.Conditions));
            var speaker = variant is { Speaker.Length: > 0 } ? variant.Speaker : Current.Speaker;
            return (_session.FormatText(speaker), _session.FormatText(variant?.Text ?? Current.Text));
        }
    }

    public string Speaker => Line.Speaker;
    public string Text => Line.Text;

    /// <summary>Choix affichés : disponibles, ou grisés quand la réplique le demande.</summary>
    public IReadOnlyList<ChoiceOption> Options =>
        Current?.Choices
            .Select(c => new ChoiceOption(c, _session.FormatText(c.Text), _session.CheckAll(c.Conditions), _session.FormatText(c.LockedText)))
            .Where(o => o.Enabled || o.Choice.ShowLocked)
            .ToList() ?? [];

    /// <summary>Choix disponibles (ceux dont les conditions sont remplies).</summary>
    public IReadOnlyList<DialogueChoice> Choices => Options.Where(o => o.Enabled).Select(o => o.Choice).ToList();

    /// <summary>La réplique attend un choix (au moins un choix affiché).</summary>
    public bool HasOptions => Options.Count > 0;

    /// <summary>Passe à la réplique suivante (réplique sans choix) : aiguillages d'abord, puis la suite normale.</summary>
    public void Continue()
    {
        if (Current is null || HasOptions) return;
        var branch = Current.Branches.FirstOrDefault(b => _session.CheckAll(b.Conditions));
        Enter(branch is not null ? branch.NextId : Current.NextId);
    }

    /// <summary>Choisit parmi les choix disponibles (index dans <see cref="Choices"/>).</summary>
    public void Choose(int index)
    {
        var choices = Choices;
        if (index < 0 || index >= choices.Count) return;
        Pick(choices[index]);
    }

    /// <summary>Choisit un choix affiché (index dans <see cref="Options"/>) ; un choix grisé ne fait rien.</summary>
    public void ChooseOption(int index)
    {
        var options = Options;
        if (index < 0 || index >= options.Count || !options[index].Enabled) return;
        Pick(options[index].Choice);
    }

    private void Pick(DialogueChoice choice)
    {
        Apply(choice.Actions);
        Enter(choice.NextId);
    }

    /// <summary>Va à une réplique : « etiquette » dans ce dialogue, « dialogue:etiquette » ou « dialogue: » (début) dans un autre.</summary>
    private void Enter(string? target)
    {
        // Garde-fou contre une boucle infinie dans un dialogue mal construit.
        if (++_steps > 500) target = null;
        Current = null;
        if (string.IsNullOrEmpty(target)) return;

        var (dialogueId, label) = GameDatabase.SplitTarget(target);
        if (dialogueId is not null)
        {
            if (!_session.Db.Dialogues.TryGetValue(dialogueId, out var other)) return;
            Dialogue = other;
            if (label.Length == 0) label = other.StartId ?? "";
        }
        Current = Dialogue.Nodes.FirstOrDefault(n => n.Id == label);
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
