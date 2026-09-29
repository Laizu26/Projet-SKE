using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Data;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>Saisie du code d'accès au mode développeur.</summary>
public sealed class DevCodePage : ContentPage
{
    public DevCodePage()
    {
        Background = Theme.PageBackground;
        var entry = new Entry
        {
            IsPassword = true,
            Keyboard = Keyboard.Numeric,
            Placeholder = "Code",
            PlaceholderColor = Theme.Muted,
            TextColor = Theme.Text,
            BackgroundColor = Theme.Panel,
            FontSize = 20,
        };
        var error = Txt("", 13, Theme.Danger);
        void TryEnter()
        {
            if (entry.Text == SkeApp.DevCode)
            {
                SkeApp.DevUnlocked = true;
                SkeApp.GoTo(new DevHomePage());
            }
            else
            {
                error.Text = "Code incorrect.";
                entry.Text = "";
            }
        }
        entry.Completed += (_, _) => TryEnter();

        Content = new VerticalStackLayout
        {
            Padding = new Thickness(24),
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                Heading("Mode développeur"),
                Muted("Entre le code d'accès."),
                entry,
                error,
                Btn("Entrer", TryEnter),
                Btn("◂ Retour", () => SkeApp.GoTo(new TitlePage())),
            },
        };
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(new TitlePage());
        return true;
    }
}

/// <summary>Base des pages d'édition : en-tête (retour, titre, suppression) et formulaire défilant.</summary>
public abstract class EditorPage : ContentPage
{
    private readonly ScrollView _scroll = new();
    private readonly Grid _header = new()
    {
        BackgroundColor = Theme.Header,
        Padding = new Thickness(8),
        ColumnSpacing = 8,
        ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
    };
    private bool _confirmDelete;

    protected EditorPage()
    {
        Background = Theme.PageBackground;
        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        root.Add(_header, 0, 0);
        root.Add(_scroll, 0, 1);
        Content = root;
    }

    protected abstract string PageTitle { get; }
    protected abstract void Build(Form form);
    protected abstract void GoBack();

    /// <summary>Suppression de l'élément (null = pas de bouton Supprimer).</summary>
    protected virtual Action? Delete => null;

    protected void Render()
    {
        _header.Children.Clear();
        var back = Form.SmallButton("◂", GoBack);
        _header.Add(back, 0, 0);
        var title = Txt(PageTitle, 16, Theme.Accent, bold: true);
        title.VerticalOptions = LayoutOptions.Center;
        _header.Add(title, 1, 0);
        if (Delete is { } delete)
        {
            _header.Add(Form.SmallButton(_confirmDelete ? "Confirmer ?" : "Supprimer", () =>
            {
                if (!_confirmDelete)
                {
                    _confirmDelete = true;
                    Render();
                    return;
                }
                delete();
                DevState.Touch();
                GoBack();
            }), 2, 0);
        }

        var form = new Form(Render);
        Build(form);
        _scroll.Content = new ContentView { Padding = new Thickness(12, 8, 12, 40), Content = form.Root };
    }

    protected override bool OnBackButtonPressed()
    {
        GoBack();
        return true;
    }
}

/// <summary>Liste des éléments d'une catégorie, avec création.</summary>
public sealed class EntityListPage<T> : ContentPage where T : class
{
    public EntityListPage(
        string title,
        List<T> items,
        Func<T, string> id,
        Func<T, string> label,
        Func<string, string, T> create,
        Func<T, Page> editor,
        Func<T, string>? subtitle = null,
        Func<T, bool>? filter = null,
        string? help = null,
        Func<T, string>? sortKey = null,
        Func<T, int>? depth = null,
        Func<T, string>? group = null,
        Func<T, string?>? parentOf = null)
    {
        Background = Theme.PageBackground;
        var stack = new VerticalStackLayout { Padding = new Thickness(12), Spacing = 6 };
        stack.Add(Row(Heading(title), Form.SmallButton("◂ Menu", () => SkeApp.GoTo(new DevHomePage()))));
        if (help is not null) stack.Add(Muted(help));

        stack.Add(Btn("+ Nouveau", async () =>
        {
            var name = await DisplayPromptAsync("Nouveau", "Nom :", "Créer", "Annuler");
            if (string.IsNullOrWhiteSpace(name)) return;
            var newId = DevState.NewId(name, items.Select(id));
            var item = create(newId, name.Trim());
            items.Add(item);
            DevState.Touch();
            SkeApp.GoTo(editor(item));
        }));

        // Recherche : filtre au fil de la frappe (nom, identifiant, détails, groupe).
        var search = new Entry { Placeholder = "🔍 Rechercher...", ClearButtonVisibility = ClearButtonVisibility.WhileEditing, FontSize = 14 };
        stack.Add(search);
        var empty = Muted("Aucun résultat.");
        empty.IsVisible = false;

        var rows = new List<(View Panel, string Text, View? Header, string Id)>();
        var headers = new List<View>();
        var ordered = items.Where(i => filter?.Invoke(i) ?? true)
            .OrderBy(i => group?.Invoke(i) ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(sortKey ?? label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        // Arbre repliable (ex : lieux et sous-lieux) : les enfants sont cachés tant que leur parent n'est pas déplié.
        var parents = new Dictionary<string, string?>();
        foreach (var i in ordered) parents.TryAdd(id(i), parentOf?.Invoke(i)); // un identifiant en double ne fait pas planter
        var childCount = ordered.Select(i => parentOf?.Invoke(i)).Where(p => p is not null && parents.ContainsKey(p))
            .GroupBy(p => p!).ToDictionary(g => g.Key, g => g.Count());
        var searching = false;
        bool AncestorsOpen(string itemId)
        {
            for (var (p, guard) = (parents.GetValueOrDefault(itemId), 0); p is not null && guard < 32; p = parents.GetValueOrDefault(p), guard++)
            {
                if (!parents.ContainsKey(p)) return true; // parent hors de la liste : on affiche
                if (!Expanded.Contains(title + "|" + p)) return false;
            }
            return true;
        }
        void RefreshTree()
        {
            if (searching || parentOf is null) return;
            foreach (var (panel, _, _, rowId) in rows) panel.IsVisible = AncestorsOpen(rowId);
        }
        string? currentGroup = null;
        View? header = null;
        foreach (var item in ordered)
        {
            var it = item;
            var itemId = id(item);
            // Regroupement (ex : compétences par pouvoir) : un titre avant chaque groupe.
            if (group is not null && group(item) is var g && g != currentGroup)
            {
                currentGroup = g;
                header = Section(g.Length > 0 ? g : "Sans groupe");
                headers.Add(header);
                stack.Add(header);
            }
            // Rangement en arbre (ex : sous-lieux décalés sous leur lieu, comme des salons dans une catégorie).
            var level = Math.Min(depth?.Invoke(item) ?? 0, 6);
            var details = id(item) + (subtitle is null ? "" : " · " + subtitle(item));
            var info = Stack(Txt((level > 0 ? "↳ " : "") + label(item), 15, Theme.Text, bold: true), Muted(details));
            var actions = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
            if (childCount.TryGetValue(itemId, out var children))
            {
                // « ▸ 3 » : déplier / replier ses 3 sous-éléments.
                var key = title + "|" + itemId;
                Button? toggle = null;
                toggle = Form.SmallButton($"{(Expanded.Contains(key) ? "▾" : "▸")} {children}", () =>
                {
                    if (!Expanded.Remove(key)) Expanded.Add(key);
                    toggle!.Text = $"{(Expanded.Contains(key) ? "▾" : "▸")} {children}";
                    RefreshTree();
                });
                actions.Add(toggle);
            }
            actions.Add(Form.SmallButton("Modifier", () => SkeApp.GoTo(editor(it))));
            var panel = Panel(Row(info, actions));
            panel.Margin = new Thickness(level * 18, 0, 0, 0);
            stack.Add(panel);
            rows.Add((panel, $"{label(item)} {details} {group?.Invoke(item)}", header, itemId));
        }
        stack.Add(empty);
        RefreshTree();
        search.TextChanged += (_, e) =>
        {
            var words = (e.NewTextValue ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            searching = words.Length > 0;
            bool Match(string text) => words.All(w => text.Contains(w, StringComparison.CurrentCultureIgnoreCase)
                || Plain(text).Contains(Plain(w), StringComparison.OrdinalIgnoreCase));
            // Pendant une recherche, tout ce qui correspond s'affiche (même replié) ; sinon, l'arbre reprend.
            foreach (var (panel, text, _, rowId) in rows) panel.IsVisible = searching ? Match(text) : parentOf is null || AncestorsOpen(rowId);
            foreach (var h in headers) h.IsVisible = rows.Any(r => r.Header == h && r.Panel.IsVisible);
            empty.IsVisible = rows.Count > 0 && rows.All(r => !r.Panel.IsVisible);
        };
        Content = new ScrollView { Content = stack };
    }

    /// <summary>Éléments dépliés (« liste|id »), gardés en revenant sur la liste.</summary>
    private static readonly HashSet<string> Expanded = [];

    /// <summary>Texte sans accents (« epee » trouve « Épée »).</summary>
    private static string Plain(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        return new string(decomposed.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(new DevHomePage());
        return true;
    }
}

/// <summary>Accueil du mode développeur : catégories à éditer, enregistrement, test, export.</summary>
public sealed class DevHomePage : ContentPage
{
    private IReadOnlyList<string>? _errors;
    private string? _message;
    private bool _confirmReset;

    public DevHomePage()
    {
        Background = Theme.PageBackground;
        Render();
    }

    private void Render()
    {
        var c = DevState.Draft;
        var stack = new VerticalStackLayout { Padding = new Thickness(12), Spacing = 6 };
        stack.Add(Heading("Mode développeur"));
        if (DevState.Restored)
            stack.Add(Txt("Brouillon retrouvé : tes modifications non enregistrées ont été récupérées.", 13, Theme.Gold700, bold: true));
        stack.Add(Muted(DevState.Dirty ? "● Modifications en cours (enregistrées automatiquement en revenant ici)" : "Tout est enregistré sur ce téléphone."));
        if (_message is not null) stack.Add(Txt(_message, 13, Theme.Good));

        stack.Add(Section("Contenu"));
        stack.Add(Nav($"PJ — personnages jouables ({c.Characters.Count})", Editors.CharacterList));
        stack.Add(Nav($"PNJ ({c.Npcs.Count})", Editors.NpcList));
        stack.Add(Nav($"Histoire — dialogues ({c.Dialogues.Count})", Editors.DialogueList));
        stack.Add(Nav($"Quêtes ({c.Quests.Count})", Editors.QuestList));
        stack.Add(Nav($"Objets ({c.Items.Count(i => i.Type != Core.Models.ItemType.Relic)})", Editors.ItemList));
        stack.Add(Nav($"Reliques ({c.Items.Count(i => i.Type == Core.Models.ItemType.Relic)})", Editors.RelicList));
        stack.Add(Nav($"Monstres ({c.Monsters.Count})", Editors.MonsterList));
        stack.Add(Nav($"Compétences ({c.Skills.Count})", Editors.SkillList));
        stack.Add(Nav($"Pouvoirs ({c.Powers.Count})", Editors.PowerList));
        stack.Add(Nav($"Passifs ({c.Passives.Count})", Editors.PassiveList));
        stack.Add(Nav($"Lieux et carte ({c.Locations.Count})", Editors.LocationList));
        stack.Add(Nav($"Départs de partie ({1 + c.ExtraStarts.Count})", () => new StartsPage()));
        stack.Add(Nav("Prologue (tutoriel)" + (c.Tutorial.Enabled ? " · proposé" : " · désactivé"), () => new TutorialEditor()));
        stack.Add(Nav("Équilibrage", () => new BalanceEditor()));

        stack.Add(Section("Monde"));
        stack.Add(Nav("Monde et textes (pays, vocabulaire)", () => new WorldEditor()));
        stack.Add(Nav("Temps et calendrier", () => new TimeEditor()));
        stack.Add(Nav("Karma", () => new ScaleEditor(karma: true)));
        stack.Add(Nav("Amitié", () => new ScaleEditor(karma: false)));
        stack.Add(Nav($"Jauges de personnage : folie... ({c.Gauges.Count})", WorldLists.GaugeList));
        stack.Add(Nav($"Campement ({c.Camp.Ranks.Count} grades, {c.Camp.Tasks.Count} tâches)", () => new CampEditor()));
        stack.Add(Nav($"Variables ({c.Variables.Count})", WorldLists.VariableList));
        stack.Add(Nav($"Banque d'images ({c.Portraits.Count})", WorldLists.ImageList));

        stack.Add(Section("Actions"));
        stack.Add(ButtonRow(
            Btn("Vérifier", () => { _errors = DevState.Validate(); _message = null; Render(); }),
            Btn("Enregistrer", Save)));
        stack.Add(Btn("Tester (partie de test)", () =>
            SkeApp.Open(() => new CharacterSelectPage(-1, DevState.DraftDatabase()), "Partie de test")));

        if (_errors is not null)
        {
            if (_errors.Count == 0) stack.Add(Txt("✓ Aucun problème détecté.", 13, Theme.Good));
            else
            {
                var box = Stack(Txt($"{_errors.Count} problème(s) :", 14, Theme.Danger, bold: true));
                foreach (var e in _errors.Take(60)) box.Add(Txt("• " + e, 12));
                stack.Add(Panel(box));
            }
        }

        stack.Add(Section("Base de données en ligne"));
        var last = CloudSync.LastSync is { } when ? $"Dernière synchro : {when:HH:mm:ss}" : "Pas encore synchronisé";
        stack.Add(Card(Stack(
            IconRow(Icon(Ico.Globe, 22, CloudSync.IsReady ? Theme.Green600 : Theme.Stone400), new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    Txt(CloudSync.IsReady ? "Synchronisation automatique active" : "Synchronisation désactivée", 14, Theme.Stone900, bold: true),
                    CloudSync.Busy ? Muted("Synchronisation en cours…", 12)
                        : Txt(CloudSync.LastStatus, 12, CloudSync.LastFailed ? Theme.Danger : Theme.Stone600, bold: CloudSync.LastFailed),
                    PendingLine(),
                    Muted($"{last} · toutes les {CloudSync.AutoInterval.TotalSeconds:0} s · {CloudSync.BackupCount} copie(s) locale(s)", 11),
                },
            }),
            ButtonRow(
                Btn("Synchroniser", SyncNow, enabled: CloudSync.IsReady && !CloudSync.Busy, selected: true),
                Btn("Configurer", () => SkeApp.GoTo(new CloudSettingsPage()))),
            Btn("Historique, récupération et journal", () => SkeApp.GoTo(new RecoveryPage())),
            Muted("Les modifications de chacun sont fusionnées élément par élément : rien n'est écrasé. " +
                  "Si un même élément a été modifié des deux côtés, la version en ligne est gardée et la tienne est mise de côté ci-dessous.", 11))));

        var conflicts = CloudSync.Conflicts;
        if (conflicts.Count > 0)
        {
            var list = new VerticalStackLayout { Spacing = 12 };
            foreach (var item in conflicts.AsEnumerable().Reverse())
            {
                var conflict = item;
                list.Add(Stack(
                    Txt($"{item.Kind} « {item.Name} »", 14, Theme.Stone900, bold: true),
                    Muted(item.LocalJson.Length == 0
                        ? $"Supprimé chez toi mais modifié en ligne : il a été gardé. ({item.When:dd/MM HH:mm})"
                        : $"Modifié des deux côtés : la version en ligne a été gardée. ({item.When:dd/MM HH:mm})", 11),
                    ButtonRow(
                        Btn("Remettre ma version", () =>
                        {
                            _message = CloudSync.RestoreMine(conflict)
                                ? $"Ta version de « {conflict.Name} » est dans le brouillon : touche « Enregistrer » pour la publier."
                                : "Impossible de restaurer cet élément.";
                            Render();
                        }, enabled: item.LocalJson.Length > 0),
                        Btn("Garder l'autre", () => { CloudSync.Dismiss(conflict); Render(); }))));
            }
            stack.Add(TitledCard(Ico.CircleAlert, $"Conflits ({conflicts.Count})", list));
        }

        stack.Add(Section("Envoyer / recevoir le contenu"));
        stack.Add(Muted("Exporte le fichier ou copie le texte pour l'envoyer : il pourra devenir le contenu officiel du jeu."));
        stack.Add(ButtonRow(Btn("Exporter le fichier", Export), Btn("Copier le texte", Copy)));
        stack.Add(ButtonRow(Btn("Importer un fichier", Import), Btn("Coller le texte", Paste)));

        stack.Add(Section("Autres"));
        stack.Add(Btn("Annuler les modifications", () => { DevState.Revert(); _message = "Modifications annulées."; Render(); },
            enabled: DevState.Dirty));
        if (!CloudSync.IsReady) stack.Add(Btn(_confirmReset ? "Confirmer : revenir au contenu d'origine ?" : "Revenir au contenu d'origine", () =>
        {
            if (!_confirmReset) { _confirmReset = true; Render(); return; }
            DevState.ResetToOfficial();
            _confirmReset = false;
            _message = "Contenu d'origine restauré.";
            Render();
        }));
        stack.Add(Btn("◂ Écran titre", () => SkeApp.GoTo(new TitlePage())));

        Content = new ScrollView { Content = stack };
    }

    private static Button Nav(string text, Func<Page> page)
    {
        var b = Btn(text, () => SkeApp.GoTo(page()));
        b.HorizontalOptions = LayoutOptions.Fill;
        return b;
    }

    private static View PendingLine()
    {
        var pending = CloudSync.PendingChanges;
        return pending == 0
            ? Txt($"✓ Tout est en ligne (révision {CloudSync.BaseRevision})", 12, Theme.Good, bold: true)
            : Txt($"● {pending} élément(s) de ce téléphone pas encore en ligne", 12, Theme.Gold700, bold: true);
    }

    private void Save()
    {
        _errors = DevState.Validate();
        var conflicts = DevState.Save();
        _message = conflicts.Count > 0
            ? $"Enregistré. {conflicts.Count} élément(s) modifié(s) entre-temps par quelqu'un d'autre : voir « Conflits »."
            : _errors.Count == 0
                ? "Enregistré ! Le jeu utilise maintenant ce contenu."
                : "Enregistré, mais il reste des problèmes (voir ci-dessous).";
        Render();
        // Publication immédiate pour que les autres appareils le reçoivent.
        SyncNow();
    }

    private async void SyncNow()
    {
        if (!CloudSync.IsReady) return;
        var task = CloudSync.SyncAsync();
        Render();
        await task;
        Render();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CloudSync.Changed += OnCloudChanged;
        // Retour à l'accueil après des modifications : enregistrement (et publication) automatique.
        if (DevState.Dirty) Save();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CloudSync.Changed -= OnCloudChanged;
    }

    private void OnCloudChanged() => Render();

    private static string ExportJson() => ContentSerializer.ToJson(DevState.Draft);

    private async void Export()
    {
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, "projet-ske-contenu.json");
            await File.WriteAllTextAsync(path, ExportJson());
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Contenu Projet SKE",
                File = new ShareFile(path, "application/json"),
            });
        }
        catch (Exception e)
        {
            _message = "Export impossible : " + e.Message;
            Render();
        }
    }

    private async void Copy()
    {
        await Clipboard.Default.SetTextAsync(ExportJson());
        _message = "Contenu copié dans le presse-papiers.";
        Render();
    }

    private async void Import()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Fichier de contenu (.json)" });
            if (file is null) return;
            using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            Load(await reader.ReadToEndAsync());
        }
        catch (Exception e)
        {
            _message = "Import impossible : " + e.Message;
            Render();
        }
    }

    private async void Paste()
    {
        var text = await Clipboard.Default.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            _message = "Le presse-papiers est vide.";
            Render();
            return;
        }
        Load(text);
    }

    private void Load(string json)
    {
        try
        {
            DevState.Replace(ContentSerializer.FromJson(json));
            _errors = DevState.Validate();
            _message = "Contenu importé. Pense à « Enregistrer » pour l'activer.";
        }
        catch (Exception e)
        {
            _message = "Fichier invalide : " + e.Message;
        }
        Render();
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(new TitlePage());
        return true;
    }
}
