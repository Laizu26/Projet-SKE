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
        string? help = null)
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

        foreach (var item in items.Where(i => filter?.Invoke(i) ?? true).OrderBy(label))
        {
            var it = item;
            var info = Stack(Txt(label(item), 15, Theme.Text, bold: true), Muted(id(item) + (subtitle is null ? "" : " · " + subtitle(item))));
            stack.Add(Panel(Row(info, Form.SmallButton("Modifier", () => SkeApp.GoTo(editor(it))))));
        }
        Content = new ScrollView { Content = stack };
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
        stack.Add(Muted(DevState.Dirty ? "● Modifications non enregistrées" : "Tout est enregistré."));
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
        stack.Add(Nav($"Lieux et carte ({c.Locations.Count})", Editors.LocationList));
        stack.Add(Nav("Départ de partie", () => new StartEditor()));
        stack.Add(Nav("Équilibrage", () => new BalanceEditor()));

        stack.Add(Section("Actions"));
        stack.Add(ButtonRow(
            Btn("Vérifier", () => { _errors = DevState.Validate(); _message = null; Render(); }),
            Btn("Enregistrer", Save)));
        stack.Add(Btn("Tester (partie de test)", () =>
            SkeApp.GoTo(new CharacterSelectPage(-1, DevState.DraftDatabase()))));

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
                    Muted(CloudSync.Busy ? "Synchronisation en cours…" : CloudSync.LastStatus, 12),
                    Muted($"{last} · toutes les {CloudSync.AutoInterval.TotalSeconds:0} s · {CloudSync.BackupCount} copie(s) locale(s)", 11),
                },
            }),
            ButtonRow(
                Btn("Synchroniser", SyncNow, enabled: CloudSync.IsReady && !CloudSync.Busy, selected: true),
                Btn("Configurer", () => SkeApp.GoTo(new CloudSettingsPage()))),
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
