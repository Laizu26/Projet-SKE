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
        - texte                 → narration (le récit, personne ne parle)
        * texte  ou  Narration: texte → narration aussi
        > texte -> etiquette    → choix (sans "->" : termine le dialogue)
        >~ texte {cond} ((raison)) → choix affiché grisé si la condition manque
        > * texte -> etiquette  → choix-narration : une action décrite, pas une parole
        > texte #id -> etiquette → choix avec un identifiant (condition {choisi dialogue replique:id})
        ? {cond} -> etiquette   → aiguillage après la réplique (le 1er qui passe gagne)
        ~ {cond} Nom: texte     → autre version de la réplique si la condition passe
        si {cond} Nom: texte    → réplique jouée seulement si la condition passe (sinon : sautée)
        si {cond} Nom: texte sinon -> etiquette → sinon, aller ailleurs
        @etiquette              → commence un nouveau bloc
        -> etiquette            → aller à un bloc (-> fin : terminer)
        -> dialogue:etiquette   → continuer dans un autre dialogue (dialogue: = son début)
        [action ...]            → effet en fin de réplique ou de choix
        {condition ...}         → condition (plusieurs {..} = toutes)
        // commentaire

        Les répliques d'un même bloc s'enchaînent toutes seules.

        Balises dans les textes : %pj% (qui parle) %heros% %pays% %monnaie%
        %heure% %date% %periode% %lieu% %or% %karma% %var:id% %amitie:pnj% %nom:id% %classe% %titre%

        Actions : [flag x] [sans_flag x] [recrute perso] [depart perso]
        [objet id 2] [prendre id 1] [or 50] [payer 50] [xp 30]
        [combat loup,loup] [quete id] [finir_quete id] [soin] [teleport lieu]
        [var x 5] [ajoute x 2] [karma 5] [karma -5 @equipe] [fixe_karma 0 perso]
        [amitie pnj 5] [amitie pnj 5 @parle] [fixe_amitie pnj 0]
        [temps 60] [attendre 8] [message texte libre]
        [deplace pnj lieu] [revele lieu] [cache lieu]
        [camp pnj] [camp pnj grade] [quitte_camp pnj] [grade pnj grade] [tache pnj tache]
        [ressource nourriture 5] [construit palissade]
        [etape quete etape] [echouer quete]
        [dialogue id] (joué juste après)
        [partie quete partie] [finir_partie quete partie] [echouer_partie quete partie]

        Conditions : {flag x} {sans_flag x} {quete_dispo id} {quete_active id}
        {quete_finie id} {objet id 2} {equipe perso} {hors_equipe perso}
        {or 50} {or < 10} {niveau 3} {var x >= 5} {karma >= 20} {karma < 0 @equipe}
        {amitie pnj >= 30} {amitie pnj > 50 @parle} {taille_equipe >= 2}
        {parle perso} {etre perso} {choisi dialogue replique:2} (2 = 2e choix, ou son #identifiant) {heure 20 6} {jour >= 3} {periode Nuit} {jour_semaine Lundi}
        {mois Givrelune} {lieu id} {visite id} {connu pnj} {chance 25}
        {au_camp pnj} {grade pnj >= 2} {tache pnj rondes}
        {ressource bois >= 10} {construit palissade}
        {etape quete etape} {passe quete etape} {fin quete etape} {fin quete} {quete_echouee quete}
        {partie_dispo quete partie} {partie_active quete partie} {partie_finie quete partie} {partie_echouee quete partie}
        {!flag x} = sauf si · {flag a | flag b} = l'un ou l'autre · {flag a & karma > 0 | flag b}
        Qui : @parle (par défaut), @heros, @equipe, ou l'identifiant d'un PJ.
        """;

    // Signature des arguments : a = identifiant, b = second identifiant, n = nombre, m = second nombre,
    // o = comparaison + nombre (« >= 5 »), t = texte libre jusqu'à la fin.
    private static readonly (string Word, ActionType Type, string Sig)[] ActionWords =
    [
        ("flag", ActionType.SetFlag, "a"), ("sans_flag", ActionType.ClearFlag, "a"), ("recrute", ActionType.Recruit, "a"),
        ("depart", ActionType.LeaveParty, "a"),
        ("objet", ActionType.GiveItem, "an"), ("prendre", ActionType.TakeItem, "an"), ("or", ActionType.GiveGold, "n"),
        ("payer", ActionType.TakeGold, "n"), ("xp", ActionType.GiveXp, "n"), ("combat", ActionType.StartBattle, "a"),
        ("quete", ActionType.StartQuest, "a"), ("finir_quete", ActionType.CompleteQuest, "a"), ("soin", ActionType.HealParty, ""),
        ("teleport", ActionType.Teleport, "a"),
        ("var", ActionType.SetVariable, "an"), ("ajoute", ActionType.AddVariable, "an"),
        ("karma", ActionType.AddKarma, "na"), ("fixe_karma", ActionType.SetKarma, "na"),
        ("amitie", ActionType.AddFriendship, "anb"), ("fixe_amitie", ActionType.SetFriendship, "anb"),
        ("temps", ActionType.AdvanceTime, "n"), ("attendre", ActionType.WaitUntilHour, "n"),
        ("message", ActionType.ShowMessage, "t"),
        ("deplace", ActionType.MoveNpc, "ab"), ("revele", ActionType.RevealLocation, "a"), ("cache", ActionType.HideLocation, "a"),
        ("camp", ActionType.JoinCamp, "ab"), ("quitte_camp", ActionType.LeaveCamp, "a"),
        ("grade", ActionType.SetCampRank, "ab"), ("tache", ActionType.SetCampTask, "ab"),
        ("ressource", ActionType.AddCampResource, "an"), ("construit", ActionType.BuildCampBuilding, "a"),
        ("etape", ActionType.SetQuestStage, "ab"),
        ("dialogue", ActionType.StartDialogue, "a"),
        ("partie", ActionType.StartQuestPart, "ab"), ("finir_partie", ActionType.CompleteQuestPart, "ab"),
        ("echouer_partie", ActionType.FailQuestPart, "ab"), ("echouer", ActionType.FailQuest, "a"),
    ];

    private static readonly (string Word, ConditionType Type, string Sig)[] ConditionWords =
    [
        ("flag", ConditionType.FlagSet, "a"), ("sans_flag", ConditionType.FlagNotSet, "a"),
        ("quete_dispo", ConditionType.QuestNotStarted, "a"), ("quete_active", ConditionType.QuestActive, "a"),
        ("quete_finie", ConditionType.QuestCompleted, "a"), ("objet", ConditionType.HasItem, "an"),
        ("equipe", ConditionType.InParty, "a"), ("hors_equipe", ConditionType.NotInParty, "a"),
        ("or", ConditionType.GoldAtLeast, "n"), ("niveau", ConditionType.LevelAtLeast, "n"),
        ("or", ConditionType.Gold, "o"), ("niveau", ConditionType.Level, "o"),
        ("var", ConditionType.Variable, "ao"), ("karma", ConditionType.Karma, "oa"),
        ("amitie", ConditionType.Friendship, "aob"), ("taille_equipe", ConditionType.PartySize, "o"),
        ("parle", ConditionType.Speaker, "a"), ("etre", ConditionType.IsHero, "a"), ("choisi", ConditionType.ChoiceMade, "ab"), ("heure", ConditionType.HourBetween, "nm"),
        ("jour", ConditionType.Day, "o"), ("periode", ConditionType.Period, "t"),
        ("jour_semaine", ConditionType.WeekDay, "t"), ("mois", ConditionType.Month, "t"),
        ("lieu", ConditionType.AtLocation, "a"), ("visite", ConditionType.Visited, "a"),
        ("connu", ConditionType.MetNpc, "a"), ("chance", ConditionType.Chance, "n"),
        ("au_camp", ConditionType.CampMember, "a"), ("grade", ConditionType.CampRank, "ao"), ("tache", ConditionType.CampTask, "ab"),
        ("ressource", ConditionType.CampResource, "ao"), ("construit", ConditionType.CampBuilt, "a"),
        ("partie_dispo", ConditionType.QuestPartNotStarted, "ab"), ("partie_active", ConditionType.QuestPartActive, "ab"),
        ("partie_finie", ConditionType.QuestPartCompleted, "ab"), ("partie_echouee", ConditionType.QuestPartFailed, "ab"),
        ("etape", ConditionType.QuestAtStage, "ab"), ("passe", ConditionType.QuestStageReached, "ab"),
        ("fin", ConditionType.QuestEnding, "ab"), ("quete_echouee", ConditionType.QuestFailed, "a"),
    ];

    private static readonly (string Symbol, CompareOp Op)[] Operators =
    [
        (">=", CompareOp.AtLeast), ("<=", CompareOp.AtMost), ("!=", CompareOp.NotEqual),
        ("=", CompareOp.Equal), (">", CompareOp.Greater), ("<", CompareOp.Less),
    ];

    private static bool IsOperator(string? token) => token is not null && Operators.Any(o => o.Symbol == token);

    /// <summary>« si {condition}... » en début de réplique (les accolades qui suivent font partie de la condition).</summary>
    /// <summary>« #identifiant » à la fin du texte d'un choix.</summary>
    [GeneratedRegex(@"\s#([\w-]+)\s*$")]
    private static partial Regex ChoiceIdTag();

    [GeneratedRegex(@"^si\s*((?:\{[^}]*\}\s*)+)", RegexOptions.IgnoreCase)]
    private static partial Regex OnlyIf();

    /// <summary>« sinon -> etiquette » en fin de réplique conditionnelle.</summary>
    [GeneratedRegex(@"\s+sinon\s*->\s*(\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Else();

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex ActionTag();

    [GeneratedRegex(@"\{([^}]*)\}")]
    private static partial Regex ConditionTag();

    [GeneratedRegex(@"\(\((.*?)\)\)")]
    private static partial Regex LockedTag();

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

            if (line.StartsWith('?'))
            {
                if (last is null) { errors.Add($"Ligne {lineNumber} : aiguillage sans réplique avant"); continue; }
                var body = line[1..];
                var branch = new DialogueBranch { Conditions = ParseConditions(ref body, lineNumber, errors) };
                var jump = Jump().Match(body);
                if (!jump.Success) { errors.Add($"Ligne {lineNumber} : aiguillage sans « -> etiquette »"); continue; }
                var target = jump.Groups[1].Value;
                branch.NextId = IsEnd(target) ? null : target;
                last.Branches.Add(branch);
                continue;
            }

            if (line.StartsWith('~'))
            {
                if (last is null) { errors.Add($"Ligne {lineNumber} : variante sans réplique avant"); continue; }
                var body = line[1..];
                var variant = new TextVariant { Conditions = ParseConditions(ref body, lineNumber, errors) };
                var (speaker, text) = SplitSpeaker(body.Trim());
                variant.Speaker = speaker;
                variant.Text = text.Trim();
                last.Variants.Add(variant);
                continue;
            }

            if (line.StartsWith('>'))
            {
                if (last is null) { errors.Add($"Ligne {lineNumber} : choix sans réplique avant"); continue; }
                var showLocked = line.StartsWith(">~");
                var body = line[(showLocked ? 2 : 1)..];
                var choice = new DialogueChoice
                {
                    ShowLocked = showLocked,
                    Actions = ParseActions(ref body, lineNumber, errors),
                    Conditions = ParseConditions(ref body, lineNumber, errors),
                };
                var locked = LockedTag().Match(body);
                if (locked.Success)
                {
                    choice.LockedText = locked.Groups[1].Value.Trim();
                    body = LockedTag().Replace(body, "");
                }
                var jump = Jump().Match(body);
                if (jump.Success)
                {
                    var target = jump.Groups[1].Value;
                    choice.NextId = IsEnd(target) ? null : target;
                    body = body[..jump.Index];
                }
                choice.Text = body.Trim();
                // « > texte #id » : identifiant du choix (pour « {choisi dialogue replique:id} »).
                if (ChoiceIdTag().Match(choice.Text) is { Success: true } tag)
                {
                    choice.Id = tag.Groups[1].Value;
                    choice.Text = choice.Text[..tag.Index].TrimEnd();
                }
                // « > * texte » : choix-narration (une action décrite, pas une parole).
                if (choice.Text.StartsWith("* ") || choice.Text == "*")
                {
                    choice.Narration = true;
                    choice.Text = choice.Text.Length > 1 ? choice.Text[2..].Trim() : "";
                }
                last.Choices.Add(choice);
                chainOpen = false;
                continue;
            }

            // Réplique (personnage ou narration), éventuellement « si {condition} ... sinon -> etiquette ».
            var lineText = line;
            var actions = ParseActions(ref lineText, lineNumber, errors);
            var onlyIf = new List<Condition>();
            string? elseId = null;
            if (OnlyIf().Match(lineText) is { Success: true } si)
            {
                lineText = lineText[si.Length..].TrimStart();
                var head = si.Groups[1].Value;
                onlyIf = ParseConditions(ref head, lineNumber, errors);
                if (Else().Match(lineText) is { Success: true } otherwise)
                {
                    var target = otherwise.Groups[1].Value;
                    elseId = IsEnd(target) ? "fin" : target;
                    lineText = lineText[..otherwise.Index];
                }
            }
            var (nodeSpeaker, nodeText) = SplitSpeaker(lineText);

            string id;
            if (pendingLabel is { Length: > 0 }) id = pendingLabel;
            else if (nodes.Count == 0) id = "debut";
            else { do { id = $"_{++autoId}"; } while (used.Contains(id)); }
            if (!used.Add(id)) errors.Add($"Ligne {lineNumber} : étiquette « {id} » déjà utilisée");

            var node = new DialogueNode { Id = id, Speaker = nodeSpeaker, Text = nodeText.Trim(), Actions = actions, Conditions = onlyIf, ElseId = elseId };
            if (chainOpen && last is { NextId: null, Choices.Count: 0 }) last.NextId = id;
            nodes.Add(node);
            last = node;
            pendingLabel = null;
            chainOpen = true;
        }

        if (nodes.Count == 0) errors.Add("Le dialogue est vide.");
        var ids = nodes.Select(n => n.Id).ToHashSet();
        // Les renvois vers un autre dialogue (« dialogue:etiquette ») sont vérifiés par la validation du contenu.
        var targets = nodes.SelectMany(n => n.Branches.Select(b => b.NextId).Concat(n.Choices.Select(c => c.NextId)).Prepend(n.NextId)
            .Append(n.ElseId is "fin" ? null : n.ElseId));
        foreach (var target in targets)
            if (target is not null && !target.Contains(':') && !ids.Contains(target)) errors.Add($"Étiquette « {target} » introuvable");
        return nodes;
    }

    private static (string Speaker, string Text) SplitSpeaker(string text)
    {
        if (text.StartsWith("- ") || text == "-") return ("", text.Length > 1 ? text[2..] : "");
        if (text.StartsWith("* ") || text == "*") return ("", text.Length > 1 ? text[2..] : "");
        var colon = text.IndexOf(':');
        // « Nom: » : les deux-points suivis d'un espace (pour ne pas couper « 10:30 » ou « %var:x% »).
        if (colon > 0 && colon <= 40 && (colon + 1 >= text.Length || text[colon + 1] == ' ') && !text[..colon].Contains('%'))
        {
            var name = text[..colon].Trim();
            // « Narration: » ou « Narrateur: » : c'est le récit, pas un personnage.
            if (name.Equals("Narration", StringComparison.OrdinalIgnoreCase) || name.Equals("Narrateur", StringComparison.OrdinalIgnoreCase))
                return ("", text[(colon + 1)..].TrimStart());
            return (name, text[(colon + 1)..]);
        }
        return ("", text);
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
            var i = 1;
            foreach (var field in word.Sig)
            {
                var token = parts.ElementAtOrDefault(i);
                switch (field)
                {
                    case 'a': action.Arg = token ?? ""; i++; break;
                    case 'b': action.Arg2 = token ?? ""; i++; break;
                    case 'n': action.Amount = ParseInt(token, 1, line, errors); i++; break;
                    case 't': action.Arg = string.Join(' ', parts.Skip(i)); i = parts.Length; break;
                }
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
            var body = m.Groups[1].Value.Trim();
            if (body.Length == 0) continue;
            var alternatives = body.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var parsed = new List<Condition>();
            foreach (var alt in alternatives)
            {
                var all = new List<Condition>();
                foreach (var atom in alt.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (ParseAtom(atom, line, errors) is { } c) all.Add(c);
                if (all.Count == 1) parsed.Add(all[0]);
                else if (all.Count > 1) parsed.Add(new Condition(ConditionType.AllOf) { Children = all });
            }
            if (parsed.Count == 1) result.Add(parsed[0]);
            else if (parsed.Count > 1) result.Add(new Condition(ConditionType.AnyOf) { Children = parsed });
        }
        text = ConditionTag().Replace(text, "");
        return result;
    }

    private static Condition? ParseAtom(string atom, int line, List<string> errors)
    {
        var negate = atom.StartsWith('!');
        if (negate) atom = atom[1..].Trim();
        var parts = atom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var name = parts[0].ToLowerInvariant();
        var candidates = ConditionWords.Where(w => w.Word == name).ToList();
        if (candidates.Count == 0) { errors.Add($"Ligne {line} : condition inconnue « {parts[0]} »"); return null; }
        // « or 50 » = or minimum ; « or < 50 » = comparaison.
        var word = candidates.Count > 1 && IsOperator(parts.ElementAtOrDefault(1))
            ? candidates.First(c => c.Sig.StartsWith('o'))
            : candidates[0];

        var condition = new Condition(word.Type) { Negate = negate };
        var i = 1;
        foreach (var field in word.Sig)
        {
            var token = parts.ElementAtOrDefault(i);
            switch (field)
            {
                case 'a': condition.Arg = token ?? ""; i++; break;
                case 'b': condition.Arg2 = token ?? ""; i++; break;
                case 'n': condition.Amount = ParseInt(token, 1, line, errors); i++; break;
                case 'm': condition.Amount2 = ParseInt(token, 0, line, errors); i++; break;
                case 't': condition.Arg = string.Join(' ', parts.Skip(i)); i = parts.Length; break;
                case 'o':
                    if (IsOperator(token))
                    {
                        condition.Op = Operators.First(o => o.Symbol == token).Op;
                        i++;
                        token = parts.ElementAtOrDefault(i);
                    }
                    condition.Amount = ParseInt(token, 0, line, errors);
                    i++;
                    break;
            }
        }
        return condition;
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
            Ref(n.ElseId);
            foreach (var b in n.Branches) Ref(b.NextId);
            foreach (var c in n.Choices) Ref(c.NextId);
        }

        // Une réplique reçoit une étiquette sauf si elle suit simplement la précédente.
        var labeled = new bool[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            var prev = i > 0 ? nodes[i - 1] : null;
            var continuation = prev is { Choices.Count: 0, Branches.Count: 0 } && prev.NextId == n.Id;
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
            if (n.Conditions.Count > 0)
            {
                sb.Append("si");
                AppendConditions(sb, n.Conditions);
                sb.Append(' ');
            }
            sb.Append(n.Speaker.Length > 0 ? $"{n.Speaker}: {n.Text}" : $"- {n.Text}");
            if (n.Conditions.Count > 0 && n.ElseId is not null) sb.Append(" sinon -> ").Append(n.ElseId);
            AppendActions(sb, n.Actions);
            sb.AppendLine();

            foreach (var v in n.Variants)
            {
                sb.Append('~');
                AppendConditions(sb, v.Conditions);
                sb.Append(' ').AppendLine(v.Speaker.Length > 0 ? $"{v.Speaker}: {v.Text}" : v.Text);
            }

            foreach (var c in n.Choices)
            {
                sb.Append(c.ShowLocked ? ">~ " : "> ").Append(c.Narration ? "* " : "").Append(c.Text);
                if (c.Id.Length > 0) sb.Append(" #").Append(c.Id);
                if (c.NextId is not null) sb.Append(" -> ").Append(c.NextId);
                AppendConditions(sb, c.Conditions);
                if (c.LockedText.Length > 0) sb.Append(" ((").Append(c.LockedText).Append("))");
                AppendActions(sb, c.Actions);
                sb.AppendLine();
            }

            foreach (var b in n.Branches)
            {
                sb.Append('?');
                AppendConditions(sb, b.Conditions);
                sb.Append(" -> ").AppendLine(b.NextId ?? "fin");
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
            var word = ActionWords.First(w => w.Type == a.Type);
            var tokens = new List<string> { word.Word };
            foreach (var field in word.Sig)
            {
                switch (field)
                {
                    case 'a': tokens.Add(a.Arg); break;
                    case 'b': tokens.Add(a.Arg2); break;
                    case 'n': tokens.Add(a.Amount.ToString()); break;
                    case 't': tokens.Add(a.Arg); break;
                }
            }
            TrimDefaults(tokens, word.Sig, a.Amount);
            sb.Append(" [").Append(string.Join(' ', tokens)).Append(']');
        }
    }

    /// <summary>Retire les derniers arguments facultatifs restés à leur valeur par défaut (texte vide, quantité 1 après un identifiant).</summary>
    private static void TrimDefaults(List<string> tokens, string sig, int amount)
    {
        for (var k = sig.Length - 1; k >= 0 && tokens.Count > 1; k--)
        {
            var token = tokens[k + 1];
            var isDefault = sig[k] switch
            {
                'a' or 'b' or 't' => token.Length == 0,
                'n' => k > 0 && amount == 1,
                _ => false,
            };
            if (!isDefault) break;
            tokens.RemoveAt(k + 1);
        }
    }

    private static void AppendConditions(StringBuilder sb, IEnumerable<Condition> conditions)
    {
        foreach (var c in conditions) sb.Append(" {").Append(WriteCondition(c)).Append('}');
    }

    private static string WriteCondition(Condition c)
    {
        if (c.Type == ConditionType.AnyOf)
            return string.Join(" | ", (c.Children ?? []).Select(child =>
                child.Type == ConditionType.AllOf ? string.Join(" & ", (child.Children ?? []).Select(WriteAtom)) : WriteAtom(child)));
        if (c.Type == ConditionType.AllOf) return string.Join(" & ", (c.Children ?? []).Select(WriteAtom));
        return WriteAtom(c);
    }

    private static string WriteAtom(Condition c)
    {
        if (c.Type is ConditionType.AnyOf or ConditionType.AllOf) return WriteCondition(c);
        var word = ConditionWords.First(w => w.Type == c.Type);
        var tokens = new List<string>();
        foreach (var field in word.Sig)
        {
            switch (field)
            {
                case 'a': tokens.Add(c.Arg); break;
                case 'b': tokens.Add(c.Arg2); break;
                case 'n': tokens.Add(c.Amount.ToString()); break;
                case 'm': tokens.Add(c.Amount2.ToString()); break;
                case 't': tokens.Add(c.Arg); break;
                case 'o':
                    tokens.Add(Operators.First(o => o.Op == c.Op).Symbol);
                    tokens.Add(c.Amount.ToString());
                    break;
            }
        }
        // Retire le dernier identifiant facultatif s'il est vide, et la quantité 1 d'un objet.
        if (word.Sig.EndsWith('b') || word.Sig.EndsWith('a') && word.Sig.Length > 1)
            if (tokens.Count > 0 && tokens[^1].Length == 0) tokens.RemoveAt(tokens.Count - 1);
        if (word.Sig == "an" && c.Amount == 1) tokens.RemoveAt(tokens.Count - 1);
        return (c.Negate ? "!" : "") + string.Join(' ', tokens.Prepend(word.Word));
    }
}
