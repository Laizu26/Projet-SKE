using System.Text;
using System.Text.RegularExpressions;
using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>
/// Écriture d'un dialogue en texte simple (mode "Texte" de l'éditeur), et conversion dans les deux sens.
/// </summary>
public static partial class DialogueScript
{
    public const string Help =
        """
        Nom: texte              → réplique d'un personnage
        - texte                 → narration
        > texte -> etiquette    → choix (sans "->" : termine le dialogue)
        @etiquette              → commence un nouveau bloc
        -> etiquette            → aller à un bloc (-> fin : terminer)
        [action ...]            → effet en fin de réplique ou de choix
        {condition ...}         → condition sur un choix
        // commentaire

        Les répliques d'un même bloc s'enchaînent toutes seules.

        Actions : [flag x] [sans_flag x] [recrute perso] [objet id 2] [prendre id 1]
        [or 50] [payer 50] [xp 30] [combat loup,loup] [quete id] [finir_quete id]
        [soin] [teleport lieu]

        Conditions : {flag x} {sans_flag x} {quete_dispo id} {quete_active id}
        {quete_finie id} {objet id 2} {equipe perso} {hors_equipe perso}
        {or 50} {niveau 3}
        """;

    private static readonly (string Word, ActionType Type)[] ActionWords =
    [
        ("flag", ActionType.SetFlag), ("sans_flag", ActionType.ClearFlag), ("recrute", ActionType.Recruit),
        ("objet", ActionType.GiveItem), ("prendre", ActionType.TakeItem), ("or", ActionType.GiveGold),
        ("payer", ActionType.TakeGold), ("xp", ActionType.GiveXp), ("combat", ActionType.StartBattle),
        ("quete", ActionType.StartQuest), ("finir_quete", ActionType.CompleteQuest), ("soin", ActionType.HealParty),
        ("teleport", ActionType.Teleport),
    ];

    private static readonly (string Word, ConditionType Type)[] ConditionWords =
    [
        ("flag", ConditionType.FlagSet), ("sans_flag", ConditionType.FlagNotSet), ("quete_dispo", ConditionType.QuestNotStarted),
        ("quete_active", ConditionType.QuestActive), ("quete_finie", ConditionType.QuestCompleted), ("objet", ConditionType.HasItem),
        ("equipe", ConditionType.InParty), ("hors_equipe", ConditionType.NotInParty), ("or", ConditionType.GoldAtLeast),
        ("niveau", ConditionType.LevelAtLeast),
    ];

    /// <summary>Actions dont l'argument est un nombre (et non un identifiant).</summary>
    private static bool AmountOnly(ActionType t) => t is ActionType.GiveGold or ActionType.TakeGold or ActionType.GiveXp;
    private static bool AmountOnly(ConditionType t) => t is ConditionType.GoldAtLeast or ConditionType.LevelAtLeast;

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex ActionTag();

    [GeneratedRegex(@"\{([^}]*)\}")]
    private static partial Regex ConditionTag();

    [GeneratedRegex(@"->\s*(\S+)\s*$")]
    private static partial Regex Jump();

    // ------------------------------------------------------------------ Texte → dialogue

    public static List<DialogueNode> Parse(string script, out List<string> errors)
    {
        errors = [];
        var nodes = new List<DialogueNode>();
        var used = new HashSet<string>();
        string? pendingLabel = null;
        DialogueNode? last = null;
        var chainOpen = false;   // la réplique précédente peut s'enchaîner sur la suivante
        var autoId = 0;
        var lineNumber = 0;

        foreach (var rawLine in script.Replace("\r", "").Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            if (line.StartsWith('@'))
            {
                pendingLabel = line[1..].Trim();
                if (pendingLabel.Length == 0) errors.Add($"Ligne {lineNumber} : étiquette vide");
                chainOpen = false;
                continue;
            }

            if (line.StartsWith("->"))
            {
                if (last is null) { errors.Add($"Ligne {lineNumber} : « -> » sans réplique avant"); continue; }
                var target = line[2..].Trim();
                last.NextId = IsEnd(target) ? null : target;
                chainOpen = false;
                continue;
            }

            if (line.StartsWith('>'))
            {
                if (last is null) { errors.Add($"Ligne {lineNumber} : choix sans réplique avant"); continue; }
                var body = line[1..];
                var choice = new DialogueChoice
                {
                    Actions = ParseActions(ref body, lineNumber, errors),
                    Conditions = ParseConditions(ref body, lineNumber, errors),
                };
                var jump = Jump().Match(body);
                if (jump.Success)
                {
                    var target = jump.Groups[1].Value;
                    choice.NextId = IsEnd(target) ? null : target;
                    body = body[..jump.Index];
                }
                choice.Text = body.Trim();
                last.Choices.Add(choice);
                chainOpen = false;
                continue;
            }

            // Réplique (personnage ou narration).
            var text = line;
            var actions = ParseActions(ref text, lineNumber, errors);
            string speaker = "";
            if (text.StartsWith("- ") || text == "-")
            {
                text = text.Length > 1 ? text[2..] : "";
            }
            else
            {
                var colon = text.IndexOf(':');
                if (colon > 0 && colon <= 40)
                {
                    speaker = text[..colon].Trim();
                    text = text[(colon + 1)..];
                }
            }

            string id;
            if (pendingLabel is { Length: > 0 }) id = pendingLabel;
            else if (nodes.Count == 0) id = "debut";
            else { do { id = $"_{++autoId}"; } while (used.Contains(id)); }
            if (!used.Add(id)) errors.Add($"Ligne {lineNumber} : étiquette « {id} » déjà utilisée");

            var node = new DialogueNode { Id = id, Speaker = speaker, Text = text.Trim(), Actions = actions };
            if (chainOpen && last is { NextId: null, Choices.Count: 0 }) last.NextId = id;
            nodes.Add(node);
            last = node;
            pendingLabel = null;
            chainOpen = true;
        }

        if (nodes.Count == 0) errors.Add("Le dialogue est vide.");
        var ids = nodes.Select(n => n.Id).ToHashSet();
        foreach (var n in nodes)
        {
            if (n.NextId is { } next && !ids.Contains(next)) errors.Add($"Étiquette « {next} » introuvable");
            foreach (var c in n.Choices)
                if (c.NextId is { } cn && !ids.Contains(cn)) errors.Add($"Étiquette « {cn} » introuvable");
        }
        return nodes;
    }

    private static bool IsEnd(string target) => target is "fin" or "FIN" or "end";

    private static List<GameAction> ParseActions(ref string text, int line, List<string> errors)
    {
        var result = new List<GameAction>();
        foreach (Match m in ActionTag().Matches(text))
        {
            var parts = m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var word = ActionWords.FirstOrDefault(w => w.Word == parts[0].ToLowerInvariant());
            if (word.Word is null) { errors.Add($"Ligne {line} : action inconnue « {parts[0]} »"); continue; }
            var action = new GameAction(word.Type);
            if (AmountOnly(word.Type)) action.Amount = ParseInt(parts.ElementAtOrDefault(1), 0, line, errors);
            else
            {
                action.Arg = parts.ElementAtOrDefault(1) ?? "";
                action.Amount = ParseInt(parts.ElementAtOrDefault(2), 1, line, errors);
            }
            result.Add(action);
        }
        text = ActionTag().Replace(text, "");
        return result;
    }

    private static List<Condition> ParseConditions(ref string text, int line, List<string> errors)
    {
        var result = new List<Condition>();
        foreach (Match m in ConditionTag().Matches(text))
        {
            var parts = m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var word = ConditionWords.FirstOrDefault(w => w.Word == parts[0].ToLowerInvariant());
            if (word.Word is null) { errors.Add($"Ligne {line} : condition inconnue « {parts[0]} »"); continue; }
            var condition = new Condition(word.Type);
            if (AmountOnly(word.Type)) condition.Amount = ParseInt(parts.ElementAtOrDefault(1), 0, line, errors);
            else
            {
                condition.Arg = parts.ElementAtOrDefault(1) ?? "";
                condition.Amount = ParseInt(parts.ElementAtOrDefault(2), 1, line, errors);
            }
            result.Add(condition);
        }
        text = ConditionTag().Replace(text, "");
        return result;
    }

    private static int ParseInt(string? text, int fallback, int line, List<string> errors)
    {
        if (text is null) return fallback;
        if (int.TryParse(text, out var value)) return value;
        errors.Add($"Ligne {line} : nombre attendu au lieu de « {text} »");
        return fallback;
    }

    // ------------------------------------------------------------------ Dialogue → texte

    public static string Write(IReadOnlyList<DialogueNode> nodes)
    {
        var refs = new Dictionary<string, int>();
        void Ref(string? id) { if (id is not null) refs[id] = refs.GetValueOrDefault(id) + 1; }
        foreach (var n in nodes)
        {
            Ref(n.NextId);
            foreach (var c in n.Choices) Ref(c.NextId);
        }

        // Une réplique reçoit une étiquette sauf si elle suit simplement la précédente.
        var labeled = new bool[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            var prev = i > 0 ? nodes[i - 1] : null;
            var continuation = prev is { Choices.Count: 0 } && prev.NextId == n.Id;
            var count = refs.GetValueOrDefault(n.Id);
            labeled[i] = i == 0 ? count > 0 || n.Id != "debut" : !continuation || count > 1;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (labeled[i])
            {
                if (i > 0) sb.AppendLine();
                sb.AppendLine("@" + n.Id);
            }
            sb.Append(n.Speaker.Length > 0 ? $"{n.Speaker}: {n.Text}" : $"- {n.Text}");
            AppendActions(sb, n.Actions);
            sb.AppendLine();

            foreach (var c in n.Choices)
            {
                sb.Append("> ").Append(c.Text);
                if (c.NextId is not null) sb.Append(" -> ").Append(c.NextId);
                AppendConditions(sb, c.Conditions);
                AppendActions(sb, c.Actions);
                sb.AppendLine();
            }

            if (n.Choices.Count == 0)
            {
                var nextFollows = i + 1 < nodes.Count && !labeled[i + 1];
                if (n.NextId is null && nextFollows) sb.AppendLine("-> fin");
                else if (n.NextId is not null && !(nextFollows && nodes[i + 1].Id == n.NextId)) sb.AppendLine("-> " + n.NextId);
            }
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    private static void AppendActions(StringBuilder sb, IEnumerable<GameAction> actions)
    {
        foreach (var a in actions)
        {
            var word = ActionWords.First(w => w.Type == a.Type).Word;
            if (a.Type == ActionType.HealParty) sb.Append(" [soin]");
            else if (AmountOnly(a.Type)) sb.Append($" [{word} {a.Amount}]");
            else if (a.Amount != 1) sb.Append($" [{word} {a.Arg} {a.Amount}]");
            else sb.Append($" [{word} {a.Arg}]");
        }
    }

    private static void AppendConditions(StringBuilder sb, IEnumerable<Condition> conditions)
    {
        foreach (var c in conditions)
        {
            var word = ConditionWords.First(w => w.Type == c.Type).Word;
            if (AmountOnly(c.Type)) sb.Append($" {{{word} {c.Amount}}}");
            else if (c.Amount != 1) sb.Append($" {{{word} {c.Arg} {c.Amount}}}");
            else sb.Append($" {{{word} {c.Arg}}}");
        }
    }
}
