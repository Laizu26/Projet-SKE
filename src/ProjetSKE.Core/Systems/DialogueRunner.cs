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
    public DialogueNode? Current
    {
        get => _echo ?? _current;
        private set => _current = value;
    }
    private DialogueNode? _current;
    public bool IsFinished => Current is null;

    /// <summary>
    /// Le choix fait est joué comme une réplique (celui qui parle le dit, ou la narration le raconte) avant la suite.
    /// Activé par l'écran de dialogue ; sans lui, on passe directement à la suite.
    /// </summary>
    public bool EchoChoices { get; set; }

    /// <summary>Réplique du choix qu'on vient de faire, affichée avant la suite (voir <see cref="EchoChoices"/>).</summary>
    private DialogueNode? _echo;

    /// <summary>Monstres à combattre une fois le dialogue terminé (action "combat").</summary>
    public IReadOnlyList<string>? PendingBattle { get; private set; }

    internal DialogueRunner(GameSession session, DialogueDef dialogue)
    {
        _session = session;
        Dialogue = dialogue;
        Enter(dialogue.StartId);
    }

    /// <summary>Bulle en cours dans la réplique (une réplique peut mêler narration et paroles, affichées à part).</summary>
    private int _segment;

    /// <summary>Change à chaque nouvelle bulle affichée (nouvelle réplique, ou bulle suivante de la même réplique).</summary>
    public int Step { get; private set; }

    // Bulles calculées une fois par bulle affichée : l'effet « machine à écrire » les relit à chaque lettre.
    private List<(string Speaker, string Text)>? _segments;
    private DialogueNode? _segmentsNode;
    private int _segmentsStep = -1;

    /// <summary>Bulles de la réplique jouée (première variante dont les conditions passent), balises remplacées.</summary>
    private List<(string Speaker, string Text)> Segments
    {
        get
        {
            if (Current is null) return [("", "")];
            if (_segments is not null && ReferenceEquals(_segmentsNode, Current) && _segmentsStep == Step) return _segments;
            var variant = Current.Variants.FirstOrDefault(v => _session.CheckAll(v.Conditions));
            var speaker = variant is { Speaker.Length: > 0 } ? variant.Speaker : Current.Speaker;
            _segments = DialogueScript.Segments(speaker, variant?.Text ?? Current.Text, _session.Db.SpeakerNames)
                .Select(s => (_session.FormatText(s.Speaker), _session.FormatText(s.Text))).ToList();
            (_segmentsNode, _segmentsStep) = (Current, Step);
            return _segments;
        }
    }

    private (string Speaker, string Text) Line
    {
        get
        {
            var segments = Segments;
            return segments[Math.Clamp(_segment, 0, segments.Count - 1)];
        }
    }

    /// <summary>Encore des bulles dans cette réplique avant ses choix ou sa suite.</summary>
    public bool HasMoreSegments => Current is not null && !IsHeroLine && _segment < Segments.Count - 1;

    public string Speaker => IsHeroLine ? _session.CharacterName("@heros") : Line.Speaker;
    /// <summary>Texte dit (vide pour une réplique du héros proposée en choix : c'est au joueur de la dire).</summary>
    public string Text => IsHeroLine ? "" : Line.Text;

    /// <summary>
    /// Réplique « choix si c'est le héros » dite par le héros joué : elle est proposée au joueur comme un choix
    /// au lieu d'être dite. (Dite par un compagnon, elle se joue normalement.)
    /// </summary>
    public bool IsHeroLine => _echo is null && Current is { HeroChoice: true } && SpeakerIsHero();

    private bool SpeakerIsHero()
    {
        var speaker = Segments[0].Speaker;
        var hero = _session.CharacterName("@heros");
        if (speaker.Length == 0 || hero.Length == 0) return false;
        return speaker == hero || hero.StartsWith(speaker + " ", StringComparison.Ordinal);
    }

    // Le choix qui représente la réplique du héros (recréé à chaque nouvelle réplique).
    private DialogueChoice? _heroChoice;
    private int _heroChoiceStep = -1;

    private DialogueChoice HeroLineChoice()
    {
        if (_heroChoice is null || _heroChoiceStep != Step)
        {
            // Toute la réplique (toutes ses bulles) devient la réponse proposée.
            _heroChoice = new DialogueChoice { Id = "replique", Text = string.Join(" ", Segments.Select(s => s.Text).Where(t => t.Length > 0)) };
            _heroChoiceStep = Step;
        }
        return _heroChoice;
    }

    /// <summary>Portrait de celui qui parle (null = pas d'image). L'image choisie sur la réplique vaut pour sa première bulle.</summary>
    public PortraitDef? Portrait => Current is null ? null : _session.Db.PortraitFor(Speaker, _segment == 0 ? Current.PortraitId : null);

    /// <summary>Choix affichés : disponibles, ou grisés quand la réplique le demande.</summary>
    public IReadOnlyList<ChoiceOption> Options
    {
        get
        {
            if (HasMoreSegments || Current is null) return [];
            // Réplique « choix si c'est le héros » : dite par un compagnon, pas de choix (elle s'enchaîne) ;
            // par le héros, elle est proposée en premier, avant les autres réponses de la réplique.
            if (Current.HeroChoice && !IsHeroLine) return [];
            var options = Current.Choices
                .Select(c => new ChoiceOption(c, _session.FormatText(c.Text), _session.CheckAll(c.Conditions), _session.FormatText(c.LockedText)))
                .Where(o => o.Enabled || o.Choice.ShowLocked)
                .ToList();
            if (IsHeroLine)
            {
                var line = HeroLineChoice();
                options.Insert(0, new ChoiceOption(line, line.Text, true, ""));
            }
            return options;
        }
    }

    /// <summary>Choix disponibles (ceux dont les conditions sont remplies).</summary>
    public IReadOnlyList<DialogueChoice> Choices => Options.Where(o => o.Enabled).Select(o => o.Choice).ToList();

    /// <summary>La réplique attend un choix (au moins un choix affiché).</summary>
    public bool HasOptions => Options.Count > 0;

    /// <summary>Passe à la réplique suivante (réplique sans choix) : aiguillages d'abord, puis la suite normale.</summary>
    public void Continue()
    {
        if (Current is null) return;
        if (HasMoreSegments)
        {
            _segment++;
            Step++;
            return;
        }
        if (_echo is not null)
        {
            // Fin de la réplique du choix : place à la suite (déjà préparée quand le choix a été fait).
            _echo = null;
            _segment = 0;
            Step++;
            return;
        }
        if (HasOptions) return;
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
        // On retient le choix : la suite (ou une autre histoire, plus tard) peut en dépendre (« A choisi »).
        var heroLine = IsHeroLine && ReferenceEquals(choice, _heroChoice);
        if (Current is { } node)
            _session.State.Choices.Add($"{Dialogue.Id}:{node.Id}:{(heroLine ? "replique" : node.ChoiceKey(choice))}");
        // Le choix est dit (ou raconté) : on retient qui parle avant que la suite ne change quoi que ce soit.
        var echoText = EchoChoices ? _session.FormatText(choice.Text).Trim() : "";
        var echoSpeaker = choice.Narration ? "" : heroLine ? _session.CharacterName("@heros") : _session.CharacterName("@parle");
        Apply(choice.Actions);
        if (heroLine && Current is { } line)
        {
            // Le héros a choisi de dire sa réplique : la suite normale de la réplique (aiguillages compris).
            var branch = line.Branches.FirstOrDefault(b => _session.CheckAll(b.Conditions));
            Enter(branch is not null ? branch.NextId : line.NextId);
        }
        else Enter(choice.NextId);
        if (echoText.Length > 0)
        {
            _echo = new DialogueNode { Id = "~choix", Speaker = echoSpeaker, Text = echoText, NextId = _current?.Id };
            _segment = 0;
            Step++;
        }
    }

    /// <summary>Va à une réplique : « etiquette » dans ce dialogue, « dialogue:etiquette » ou « dialogue: » (début) dans un autre.</summary>
    private void Enter(string? target)
    {
        // Garde-fou contre une boucle infinie dans un dialogue mal construit.
        if (++_steps > 500) target = null;
        Current = null;
        _segment = 0;
        Step++;
        if (string.IsNullOrEmpty(target)) return;

        var (dialogueId, label) = GameDatabase.SplitTarget(target);
        if (dialogueId is not null)
        {
            if (!_session.Db.Dialogues.TryGetValue(dialogueId, out var other)) return;
            Dialogue = other;
            if (label.Length == 0) label = other.StartId ?? "";
        }
        Current = Dialogue.Nodes.FirstOrDefault(n => n.Id == label);
        // « Seulement si » : la réplique est sautée (vers « sinon », ou la suite normale) si ses conditions manquent.
        if (Current is { Conditions.Count: > 0 } node && !_session.CheckAll(node.Conditions))
        {
            Enter(node.ElseId ?? node.NextId);
            return;
        }
        if (Current is not null) Apply(Current.Actions);
    }

    private void Apply(IEnumerable<GameAction> actions)
    {
        foreach (var action in actions)
        {
            if (_session.Execute(action) is { } battle) PendingBattle = battle;
        }
        // Les choix peuvent débloquer un embranchement de quête : on réévalue tout de suite.
        _session.UpdateQuests();
    }
}
