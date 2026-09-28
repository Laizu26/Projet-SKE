using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>Outils communs aux écrans d'édition des dialogues : libellés lisibles, liens, test.</summary>
internal static class DialogueTools
{
    public static string Short(string text, int max = 40)
    {
        text = text.Replace('\n', ' ').Trim();
        return text.Length > max ? text[..max] + "…" : text;
    }

    public static string Who(DialogueNode n) => n.Speaker.Length > 0 ? n.Speaker : "Narration";

    /// <summary>« Bran : Tu m'aides ? » — une réplique désignée par son contenu, pas par son code.</summary>
    public static string Label(DialogueNode n, int max = 40) => $"{Who(n)} : {Short(n.Text, max)}";

    /// <summary>Répliques où un lien peut mener : celles de ce dialogue (par leur texte), puis le début des autres dialogues.</summary>
    public static List<(string Id, string Name)> Targets(DialogueDef dialogue)
    {
        var list = dialogue.Nodes.Select((n, i) => (n.Id, $"{i + 1}. {Label(n)}")).ToList();
        foreach (var other in DevState.Draft.Dialogues.Where(d => d != dialogue))
        {
            var name = other.Name.Length > 0 ? other.Name : other.Id;
            list.AddRange(other.Nodes.Select((o, i) => (i == 0 ? other.Id + ":" : other.Id + ":" + o.Id, $"↪ {name} › {(i == 0 ? "début" : Label(o, 30))}")));
        }
        return list;
    }

    /// <summary>Texte d'un lien pour l'aperçu : « → Bran : Merci ! », « → fin », « ⚠ introuvable ».</summary>
    public static (string Text, bool Broken) Describe(DialogueDef dialogue, string? target)
    {
        if (string.IsNullOrEmpty(target) || target == "fin") return ("fin du dialogue", false);
        var node = DialogueGraph.Resolve(DevState.Draft, dialogue, target);
        if (node is null) return ($"⚠ introuvable ({target})", true);
        var inOther = target.Contains(':');
        var index = dialogue.Nodes.IndexOf(node);
        return (inOther ? $"↪ {target.Split(':')[0]} › {Label(node, 30)}" : $"{index + 1}. {Label(node, 34)}", false);
    }

    /// <summary>Joue le dialogue tout de suite dans une partie de test (depuis le début, ou depuis une réplique).</summary>
    public static void Test(DialogueDef dialogue, DialogueNode? from = null)
    {
        var content = ContentSerializer.Clone(DevState.Draft);
        var copy = content.Dialogues.FirstOrDefault(d => d.Id == dialogue.Id);
        if (copy is null || copy.Nodes.Count == 0) return;
        if (from is not null && copy.Nodes.FirstOrDefault(n => n.Id == from.Id) is { } start)
        {
            // Démarrer ailleurs qu'au début : la réplique choisie passe en premier (les liens restent les mêmes).
            copy.Nodes.Remove(start);
            copy.Nodes.Insert(0, start);
        }
        var db = new GameDatabase(content);
        var hero = db.Starters.FirstOrDefault()?.Id ?? content.Characters.FirstOrDefault()?.Id;
        if (hero is null) return;
        SkeApp.Open(() =>
        {
            var page = new GamePage(GameSession.NewGame(db, hero), -1, playIntro: false);
            page.Dispatcher.Dispatch(() => page.ShowDialogue(copy.Id));
            return page;
        }, "Test du dialogue");
    }

    /// <summary>Crée une réplique juste après une autre (même personnage), et renvoie-la.</summary>
    public static DialogueNode NewAfter(DialogueDef dialogue, DialogueNode? after)
    {
        var node = new DialogueNode
        {
            Id = DevState.NewId("r", dialogue.Nodes.Select(n => n.Id)),
            Speaker = after?.Speaker ?? "",
            Text = "",
        };
        var index = after is null ? dialogue.Nodes.Count : dialogue.Nodes.IndexOf(after) + 1;
        dialogue.Nodes.Insert(index, node);
        DevState.Touch();
        return node;
    }
}

/// <summary>
/// Éditeur d'un dialogue : le « Déroulé » (une carte par réplique, avec où elle mène) ou le mode Texte.
/// Toucher une carte ouvre la réplique seule. Liens cassés et répliques isolées sont signalés.
/// </summary>
public sealed class DialogueEditor : EditorPage
{
    private readonly DialogueDef _x;
    private bool _textMode;
    private string? _script;
    private List<string> _errors = [];
    private IDispatcherTimer? _check;

    public DialogueEditor(DialogueDef x, bool textMode = false)
    {
        _x = x;
        _textMode = textMode;
        Render();
    }

    protected override string PageTitle => "Dialogue : " + (_x.Name.Length > 0 ? _x.Name : _x.Id);
    protected override Action Delete => () => DevState.Draft.Dialogues.Remove(_x);

    protected override void GoBack()
    {
        // Le texte n'est jamais perdu : on l'enregistre en quittant.
        if (_textMode && !ApplyText()) return;
        SkeApp.GoTo(Editors.DialogueList());
    }

    protected override void Build(Form f)
    {
        f.Note($"Identifiant : {_x.Id} (pour « -> {_x.Id}: » et l'effet « Dialogue : lancer »)");
        f.TextField("Nom (pour s'y retrouver)", _x.Name, v => _x.Name = v);
        f.Add(ButtonRow(
            Btn("Déroulé", () =>
            {
                if (_textMode && !ApplyText()) return;
                _textMode = false;
                Render();
            }, selected: !_textMode),
            Btn("Texte", () => { _textMode = true; _script = DialogueScript.Write(_x.Nodes); _errors = []; Render(); }, selected: _textMode),
            Btn("▶ Tester", () =>
            {
                if (_textMode && !ApplyText()) return;
                DialogueTools.Test(_x);
            })));

        if (_textMode) BuildText(f);
        else BuildFlow(f);
    }

    // ------------------------------------------------------------------ Déroulé

    private void BuildFlow(Form f)
    {
        var broken = DialogueGraph.Broken(DevState.Draft, _x);
        var isolated = DialogueGraph.Unreachable(DevState.Draft, _x);
        if (broken.Count > 0 || isolated.Count > 0)
        {
            var warn = Stack(Txt("À vérifier", 13, Theme.Danger, bold: true));
            foreach (var l in broken) warn.Add(Txt($"• {DialogueTools.Label(l.From, 30)} : {l.Kind} mène à « {l.Target} », introuvable", 12));
            foreach (var n in isolated) warn.Add(Txt($"• {DialogueTools.Label(n, 30)} : aucune réplique n'y mène", 12));
            f.Add(Panel(warn));
        }
        if (_x.Nodes.Count == 0) f.Note("Dialogue vide : ajoute une première réplique.");
        else f.Note("La première réplique est le début. Touche une réplique pour la modifier.");

        for (var i = 0; i < _x.Nodes.Count; i++) f.Add(NodeCard(_x.Nodes[i], i, isolated.Contains(_x.Nodes[i])));

        f.Add(Primary("+ Réplique", () =>
        {
            var last = _x.Nodes.LastOrDefault();
            var node = DialogueTools.NewAfter(_x, last);
            // Reliée à la précédente si celle-ci se terminait simplement.
            if (last is { NextId: null, Choices.Count: 0, Branches.Count: 0 }) last.NextId = node.Id;
            SkeApp.GoTo(new DialogueNodeEditor(_x, node));
        }));
    }

    private View NodeCard(DialogueNode n, int index, bool isolated)
    {
        var narration = n.Speaker.Length == 0;
        var body = new VerticalStackLayout { Spacing = 5 };

        var head = new HorizontalStackLayout { Spacing = 8 };
        head.Add(Badge((index + 1).ToString(), index == 0 ? Theme.Gold700 : Theme.Stone600));
        head.Add(Caps(narration ? "Narration" : n.Speaker, 10, narration ? Theme.Stone500 : Theme.Gold700));
        if (n.Conditions.Count > 0) head.Add(Badge("🔒 si", Theme.Stone500));
        if (n.Variants.Count > 0) head.Add(Badge($"✦ {n.Variants.Count}", Theme.Stone500));
        var effects = n.Actions.Count + n.Choices.Sum(c => c.Actions.Count);
        if (effects > 0) head.Add(Badge($"⚙ {effects}", Theme.Stone500));
        if (isolated) head.Add(Badge("isolée", Theme.Red600));
        body.Add(head);

        body.Add(new Label
        {
            Text = n.Text.Length > 0 ? DialogueTools.Short(n.Text, 160) : "(texte vide)",
            FontSize = 14,
            FontAttributes = narration ? FontAttributes.Italic : FontAttributes.None,
            TextColor = n.Text.Length > 0 ? Theme.Text : Theme.Muted,
            MaxLines = 3,
            LineBreakMode = LineBreakMode.TailTruncation,
        });

        // Où mène la réplique : ses choix, ou sa suite.
        if (n.Choices.Count > 0)
        {
            foreach (var c in n.Choices)
            {
                var (target, broken) = DialogueTools.Describe(_x, c.NextId);
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                row.Add(Txt($"{(c.Narration ? "✦" : "›")} {DialogueTools.Short(c.Text, 40)}  →  {target}", 12, broken ? Theme.Danger : Theme.Stone600), 0, 0);
                if (c.NextId is null)
                {
                    var choice = c;
                    row.Add(Form.SmallButton("+ Suite", () =>
                    {
                        var node = DialogueTools.NewAfter(_x, n);
                        choice.NextId = node.Id;
                        SkeApp.GoTo(new DialogueNodeEditor(_x, node));
                    }), 1, 0);
                }
                body.Add(row);
            }
        }
        else
        {
            var (target, broken) = DialogueTools.Describe(_x, n.NextId);
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            var prefix = n.Branches.Count > 0 ? $"⑂ {n.Branches.Count} aiguillage(s), sinon → " : "→ ";
            row.Add(Txt(prefix + target, 12, broken ? Theme.Danger : Theme.Stone600), 0, 0);
            if (n.NextId is null)
            {
                row.Add(Form.SmallButton("+ Suite", () =>
                {
                    var node = DialogueTools.NewAfter(_x, n);
                    n.NextId = node.Id;
                    SkeApp.GoTo(new DialogueNodeEditor(_x, node));
                }), 1, 0);
            }
            body.Add(row);
        }

        // Ranger, dupliquer, supprimer.
        var tools = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };
        if (index > 0) tools.Add(Form.SmallButton("▲", () => Move(n, -1)));
        if (index < _x.Nodes.Count - 1) tools.Add(Form.SmallButton("▼", () => Move(n, 1)));
        tools.Add(Form.SmallButton("⧉", () =>
        {
            var copy = ContentSerializer.Clone(n);
            copy.Id = DevState.NewId("r", _x.Nodes.Select(o => o.Id));
            _x.Nodes.Insert(_x.Nodes.IndexOf(n) + 1, copy);
            DevState.Touch();
            Render();
        }));
        tools.Add(Form.SmallButton("✕", () => _ = Remove(n)));
        body.Add(tools);

        var card = Card(body, isolated ? Color.FromArgb("#FEF2F2") : null, index == 0 ? Theme.Gold600 : null);
        card.Padding = new Thickness(12, 10);
        return OnTap(card, () => SkeApp.GoTo(new DialogueNodeEditor(_x, n)));
    }

    private void Move(DialogueNode n, int step)
    {
        var i = _x.Nodes.IndexOf(n);
        var j = i + step;
        if (j < 0 || j >= _x.Nodes.Count) return;
        (_x.Nodes[i], _x.Nodes[j]) = (_x.Nodes[j], _x.Nodes[i]);
        DevState.Touch();
        Render();
    }

    private async Task Remove(DialogueNode n)
    {
        var incoming = DialogueGraph.Incoming(DevState.Draft, _x, n).Count;
        var message = incoming == 0
            ? $"Supprimer « {DialogueTools.Label(n)} » ?"
            : $"Supprimer « {DialogueTools.Label(n)} » ?\n\n{incoming} lien(s) y mènent : ils mèneront à la fin du dialogue.";
        if (!await DisplayAlertAsync("Supprimer la réplique", message, "Supprimer", "Annuler")) return;
        DialogueGraph.Remove(DevState.Draft, _x, n);
        DevState.Touch();
        Render();
    }

    // ------------------------------------------------------------------ Mode texte

    private void BuildText(Form f)
    {
        _script ??= DialogueScript.Write(_x.Nodes);
        var editor = new Editor
        {
            Text = _script,
            TextColor = Theme.Text,
            BackgroundColor = Theme.Panel,
            FontSize = 14,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 300,
        };
        var status = new VerticalStackLayout { Spacing = 2 };
        void ShowStatus()
        {
            status.Children.Clear();
            if (_errors.Count == 0) status.Add(Txt("✓ Aucune erreur. Le texte est enregistré en quittant ou en passant au Déroulé.", 12, Theme.Good));
            else
            {
                status.Add(Txt("À corriger :", 13, Theme.Danger, bold: true));
                foreach (var e in _errors) status.Add(Txt("• " + e, 12));
            }
        }
        // Vérification pendant l'écriture (une demi-seconde après la dernière frappe).
        _check ??= Dispatcher.CreateTimer();
        _check.Interval = TimeSpan.FromMilliseconds(500);
        _check.IsRepeating = false;
        _check.Tick += (_, _) =>
        {
            DialogueScript.Parse(_script ?? "", out _errors);
            ShowStatus();
        };
        editor.TextChanged += (_, e) =>
        {
            _script = e.NewTextValue ?? "";
            _check.Stop();
            _check.Start();
        };
        DialogueScript.Parse(_script, out _errors);
        ShowStatus();
        f.Add(editor);
        f.Add(Panel(status));
        f.Add(Btn("Appliquer maintenant", () => { if (ApplyText()) Render(); }));
        f.Header("Aide");
        f.Add(Panel(Txt(DialogueScript.Help, 12, Theme.Muted)));
    }

    /// <summary>Enregistre le texte dans le dialogue. Faux si le texte est vide ou illisible (on reste sur place).</summary>
    private bool ApplyText()
    {
        if (_script is null) return true;
        var nodes = DialogueScript.Parse(_script, out _errors);
        if (nodes.Count == 0)
        {
            Render();
            return false;
        }
        _x.Nodes = nodes;
        DevState.Touch();
        return true;
    }
}

/// <summary>
/// Une réplique, seule : qui parle, le texte, les choix et la suite d'abord ; le reste (conditions, variantes,
/// effets, image, étiquette) dans des sections repliables.
/// </summary>
public sealed class DialogueNodeEditor : EditorPage
{
    private readonly DialogueDef _d;
    private readonly DialogueNode _x;
    private static readonly HashSet<string> Open = [];

    public DialogueNodeEditor(DialogueDef dialogue, DialogueNode node)
    {
        _d = dialogue;
        _x = node;
        Render();
    }

    private int Index => _d.Nodes.IndexOf(_x);
    protected override string PageTitle => $"{(_d.Name.Length > 0 ? _d.Name : _d.Id)} › réplique {Index + 1}";
    protected override void GoBack() => SkeApp.GoTo(new DialogueEditor(_d));

    protected override void Build(Form f)
    {
        var index = Index;
        f.Add(ButtonRow(
            Btn("◂ Précédente", () => SkeApp.GoTo(new DialogueNodeEditor(_d, _d.Nodes[index - 1])), enabled: index > 0),
            Btn("▶ Tester d'ici", () => DialogueTools.Test(_d, _x)),
            Btn("Suivante ▸", () => SkeApp.GoTo(new DialogueNodeEditor(_d, _d.Nodes[index + 1])), enabled: index < _d.Nodes.Count - 1)));

        Speaker(f);
        f.TextField(_x.Speaker.Length == 0 ? "Texte du récit" : "Ce qu'il dit", _x.Text, v => _x.Text = v, multiline: true);

        // Choix du joueur.
        f.Header($"Choix du joueur ({_x.Choices.Count})");
        if (_x.Choices.Count == 0) f.Note("Sans choix, le dialogue continue tout seul vers la suite ci-dessous.");
        var targets = DialogueTools.Targets(_d);
        for (var i = 0; i < _x.Choices.Count; i++) Choice(f, _x.Choices[i], i, targets);
        f.Add(Btn("+ Choix", () =>
        {
            _x.Choices.Add(new DialogueChoice { Text = "", Id = DevState.NewId("choix", _x.Choices.Select(o => o.Id)) });
            DevState.Touch();
            Render();
        }));

        // Suite (sans choix).
        if (_x.Choices.Count == 0)
        {
            f.Header("Ensuite");
            f.RefField("Réplique suivante (aucune = fin du dialogue)", _x.NextId, targets.Where(t => t.Id != _x.Id), v => _x.NextId = v, rerender: true);
            if (_x.NextId is null)
                f.Add(Btn("+ Créer la réplique suivante", () =>
                {
                    var node = DialogueTools.NewAfter(_d, _x);
                    _x.NextId = node.Id;
                    SkeApp.GoTo(new DialogueNodeEditor(_d, node));
                }));
            Section(f, "branches", "Aiguillages (aller ailleurs selon la situation)", _x.Branches.Count, sf =>
            {
                sf.Note("Testés avant la suite normale : le premier dont les conditions passent décide où aller.");
                sf.ObjectList("Aiguillages", _x.Branches, () => new DialogueBranch(), (bf, b, _) =>
                {
                    bf.Conditions("Si", b.Conditions);
                    bf.RefField("Aller à (aucune = fin)", b.NextId, targets, v => b.NextId = v);
                }, "+ Aiguillage");
            });
        }

        Section(f, "conditions", "Seulement si… (réplique jouée sous conditions)", _x.Conditions.Count, sf =>
        {
            sf.Conditions("Seulement si (vide = toujours)", _x.Conditions);
            if (_x.Conditions.Count > 0)
                sf.RefField("Sinon, aller à (aucune = passer à la suite)", _x.ElseId,
                    targets.Where(o => o.Id != _x.Id).Prepend(("fin", "Terminer le dialogue")), v => _x.ElseId = v);
        });
        Section(f, "variants", "Variantes du texte (selon la situation)", _x.Variants.Count, sf =>
            sf.ObjectList("Variantes", _x.Variants, () => new TextVariant { Text = _x.Text }, (vf, v, _) =>
            {
                vf.Conditions("Si", v.Conditions);
                vf.TextField("Qui parle (vide = le même)", v.Speaker, x => v.Speaker = x);
                vf.TextField("Texte à la place", v.Text, x => v.Text = x, multiline: true);
            }, "+ Variante"));
        Section(f, "effects", "Effets de la réplique (quête, objet, dialogue, combat...)", _x.Actions.Count, sf =>
            sf.Actions("Effets", _x.Actions));
        Section(f, "image", _x.Speaker.Length == 0 ? "Illustration (image au-dessus du récit)" : "Portrait", _x.PortraitId is null ? 0 : 1, sf =>
            sf.RefField(_x.Speaker.Length == 0 ? "Illustration" : "Portrait (aucun = celui de « Qui parle »)", _x.PortraitId, DevState.Portraits, v => _x.PortraitId = v));
        Section(f, "advanced", "Avancé : étiquette", 0, sf =>
        {
            sf.Note("L'étiquette sert aux liens en mode texte (« -> etiquette ») et dans les autres dialogues (« -> dialogue:etiquette »).");
            sf.TextField("Étiquette", _x.Id, v => RenameId(v.Trim()));
        });
    }

    /// <summary>Qui parle : un PJ ou PNJ de la liste (pas de faute de frappe), le PJ qui parle, la narration, ou un autre nom.</summary>
    private void Speaker(Form f)
    {
        var names = new List<(string Id, string Name)>
        {
            ("@narration", "Narration (personne ne parle)"),
            ("%pj%", "Le PJ qui parle (%pj%)"),
        };
        names.AddRange(DevState.Draft.Characters.Select(c => (c.Name, "PJ · " + c.Name)));
        names.AddRange(DevState.Draft.Npcs.Select(n => (n.Name, "PNJ · " + n.Name)));
        names = names.DistinctBy(n => n.Id).ToList();
        names.Add(("@autre", "Autre nom…"));
        var current = _x.Speaker.Length == 0 ? "@narration" : names.Any(n => n.Id == _x.Speaker) ? _x.Speaker : "@autre";
        f.RefField("Qui parle", current, names, v =>
        {
            _x.Speaker = v switch
            {
                "@narration" or null => "",
                "@autre" => current == "@autre" ? _x.Speaker : "Inconnu",
                _ => v,
            };
        }, allowNone: false, rerender: true);
        if (current == "@autre") f.TextField("Nom affiché", _x.Speaker, v => _x.Speaker = v);
    }

    private void Choice(Form f, DialogueChoice c, int i, List<(string Id, string Name)> targets)
    {
        var box = new Form(Render);
        box.TextField(c.Narration ? "Action décrite (ex : Tu t'éloignes sans un mot.)" : $"Choix {i + 1}", c.Text, v => c.Text = v);
        box.RefField("Mène à (aucune = fin du dialogue)", c.NextId, targets, v => c.NextId = v, rerender: true);
        var actions = new HorizontalStackLayout { Spacing = 4 };
        if (c.NextId is null)
            actions.Add(Form.SmallButton("+ Créer la suite", () =>
            {
                var node = DialogueTools.NewAfter(_d, _x);
                c.NextId = node.Id;
                SkeApp.GoTo(new DialogueNodeEditor(_d, node));
            }));
        if (i > 0) actions.Add(Form.SmallButton("▲", () => { (_x.Choices[i - 1], _x.Choices[i]) = (_x.Choices[i], _x.Choices[i - 1]); DevState.Touch(); Render(); }));
        actions.Add(Form.SmallButton("✕", () => { _x.Choices.RemoveAt(i); DevState.Touch(); Render(); }));
        box.Add(actions);
        var key = "choice" + i;
        var extras = c.Conditions.Count + c.Actions.Count + (c.Narration ? 1 : 0);
        Section(box, key, "Options du choix (conditions, effets, narration, identifiant)", extras, cf =>
        {
            cf.BoolField("Narration (une action décrite, pas une parole)", c.Narration, v => c.Narration = v, rerender: true);
            cf.Conditions("Proposé seulement si", c.Conditions);
            if (c.Conditions.Count > 0)
            {
                cf.BoolField("Sinon : l'afficher grisé", c.ShowLocked, v => c.ShowLocked = v, rerender: true);
                if (c.ShowLocked) cf.TextField("Raison affichée", c.LockedText, v => c.LockedText = v);
            }
            cf.Actions("Effets du choix", c.Actions);
            cf.TextField($"Identifiant (condition « A choisi » ; vide = n° {i + 1})", c.Id, v => c.Id = v.Trim());
        });
        f.Add(Panel(box.Root));
    }

    /// <summary>Section repliable (fermée par défaut) : son titre montre combien d'éléments elle contient.</summary>
    private void Section(Form f, string key, string title, int count, Action<Form> build)
    {
        var open = Open.Contains(key);
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { Txt(title + (count > 0 ? $"  ({count})" : ""), 13, count > 0 ? Theme.Accent : Theme.Text, bold: true) },
        };
        header.Add(Txt(open ? "▾" : "▸", 16, Theme.Muted), 1, 0);
        var card = Card(header);
        card.Padding = new Thickness(12, 10);
        f.Add(OnTap(card, () =>
        {
            if (!Open.Remove(key)) Open.Add(key);
            Render();
        }));
        if (open) build(f);
    }

    /// <summary>Change l'étiquette et met à jour les liens qui y menaient (dans ce dialogue et les autres).</summary>
    private void RenameId(string id)
    {
        if (id.Length == 0 || id == _x.Id || _d.Nodes.Any(n => n.Id == id)) return;
        var old = _x.Id;
        foreach (var d in DevState.Draft.Dialogues)
            foreach (var link in d.Nodes.SelectMany(DialogueGraph.Links))
            {
                if (d == _d && link.Target == old) link.Set(id);
                else if (link.Target == $"{_d.Id}:{old}") link.Set($"{_d.Id}:{id}");
            }
        _x.Id = id;
    }
}
