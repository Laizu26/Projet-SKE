using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>Écran titre, dans l'esprit de l'écran d'accès de Service Impérial.</summary>
public sealed class TitlePage : ContentPage
{
    public TitlePage()
    {
        Background = Theme.Diagonal(Night.Stone900, Night.Stone950);

        var band = new VerticalStackLayout
        {
            BackgroundColor = Night.Stone900,
            Spacing = 0,
            Children =
            {
                GoldLine(4),
                new VerticalStackLayout
                {
                    Padding = new Thickness(24, 30, 24, 26),
                    Spacing = 12,
                    Children =
                    {
                        Emblem(Ico.Shield, 92),
                        new Label
                        {
                            Text = "PROJET SKE", FontFamily = "serif", FontSize = 30, FontAttributes = FontAttributes.Bold,
                            TextColor = Night.Stone100, CharacterSpacing = 6, HorizontalTextAlignment = TextAlignment.Center,
                        },
                        new Label
                        {
                            Text = SkeApp.Db.Content.Title.ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold,
                            TextColor = Night.Stone500, CharacterSpacing = 5, HorizontalTextAlignment = TextAlignment.Center,
                        },
                    },
                },
            },
        };

        // Une partie existe déjà : « Charger » devient le bouton principal (même place, style inversé).
        var hasSave = Enumerable.Range(0, Core.Systems.SaveService.SlotCount).Any(SkeApp.Saves.Exists);
        void NewGame() => SkeApp.GoTo(new SlotPage(newGame: true));
        void LoadGame() => SkeApp.GoTo(new SlotPage(newGame: false));

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(24, 26),
            Spacing = 14,
            BackgroundColor = Theme.Parchment,
            Children =
            {
                IconCaps(Ico.Swords, "Nouvelle aventure", Theme.Stone400),
                hasSave ? Btn("Nouvelle partie", NewGame) : Primary("Nouvelle partie  ▸", NewGame),
                IconCaps(Ico.Save, "Reprendre", Theme.Stone400),
                hasSave ? Primary("Charger une partie  ▸", LoadGame) : Btn("Charger une partie", LoadGame),
            },
        };

        var footer = new ContentView
        {
            BackgroundColor = Theme.Stone200,
            Padding = new Thickness(12),
            Content = new Label
            {
                Text = $"VERSION 1.0.{Updates.CurrentBuild} · {Updates.PlatformName}".ToUpperInvariant(), FontSize = 9, FontAttributes = FontAttributes.Bold, CharacterSpacing = 3,
                TextColor = Theme.Stone400, HorizontalTextAlignment = TextAlignment.Center,
            },
        };

        var card = new Border
        {
            Content = new VerticalStackLayout { Spacing = 0, Children = { band, body, footer } },
            BackgroundColor = Theme.Parchment,
            Stroke = Night.Stone800,
            StrokeThickness = 4,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = 0,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 14), Radius = 30, Opacity = 0.6f },
        };

        var dev = new Border
        {
            BackgroundColor = Night.Stone900,
            Stroke = Theme.Gold700,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 12),
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                Children = { Icon(Ico.WandSparkles, 16, Theme.Gold500), Caps("Mode développeur", 11, Theme.Gold500) },
            },
        };
        OnTap(dev, () => SkeApp.GoTo(SkeApp.DevUnlocked ? new Dev.DevHomePage() : (Page)new Dev.DevCodePage()));

        var column = new VerticalStackLayout
        {
            Padding = new Thickness(22, 40),
            Spacing = 18,
            VerticalOptions = LayoutOptions.Center,
            Children = { card, dev, DarkModeButton(() => SkeApp.GoTo(new TitlePage())) },
        };

        // Le jeu a planté la dernière fois : on affiche le rapport pour pouvoir l'envoyer.
        if (CrashReporter.Last is { } crash)
        {
            var firstLines = string.Join("\n", crash.Split('\n').Take(6));
            Border? report = null;
            report = Card(Stack(
                IconCaps(Ico.CircleAlert, "Le jeu a planté la dernière fois", Theme.Red600),
                Txt(firstLines, 11, Theme.Stone700),
                ButtonRow(
                    Btn("Copier le rapport", async () => await Clipboard.Default.SetTextAsync(crash), selected: true),
                    Btn("Effacer", () => { CrashReporter.Clear(); report!.IsVisible = false; }))));
            column.Insert(0, report);
        }

        // Mise à jour publiée sur GitHub : carte en tête de l'écran.
        column.Insert(0, _updateHost);
        RenderUpdate();

        Content = new ScrollView { Content = column };
    }

    /// <summary>Interrupteur du mode sombre (écran titre et Menu du jeu), puis on redessine l'écran.</summary>
    internal static Button DarkModeButton(Action redraw)
    {
        var button = Pill((Theme.Dark ? "☀  Mode clair" : "☾  Mode sombre"), () =>
        {
            Theme.SetDark(!Theme.Dark);
            redraw();
        });
        button.HorizontalOptions = LayoutOptions.Center;
        return button;
    }

    private readonly ContentView _updateHost = new();
    private string? _updateMessage;
    private double? _progress;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Updates.Changed += RenderUpdate;
        if (!Dev.AutoTest.Requested) _ = Updates.CheckAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Updates.Changed -= RenderUpdate;
    }

    private void RenderUpdate()
    {
        if (!Updates.IsAvailable || Updates.Latest is not { } release)
        {
            _updateHost.Content = null;
            return;
        }
        var notes = string.Join("\n", release.Notes.Split('\n').Where(l => l.Trim().Length > 0).Take(4));
        var box = Stack(
            IconCaps(Ico.Sparkles, "Mise à jour disponible", Theme.Gold700),
            Txt(release.Name, 16, Theme.Stone900, bold: true));
        if (notes.Length > 0) box.Add(Txt(notes, 12, Theme.Stone600));
        if (_progress is { } p) box.Add(Bar("", (int)(p * 100), 100, Theme.Gold500, 8));
        if (_updateMessage is not null) box.Add(Txt(_updateMessage, 12, Theme.Stone700));
        box.Add(Primary(_progress is null ? "Installer la mise à jour" : "Téléchargement…", async () =>
        {
            if (_progress is not null) return;
            _progress = 0;
            _updateMessage = null;
            RenderUpdate();
            var progress = new Progress<double>(v => { _progress = v; RenderUpdate(); });
            _updateMessage = await Updates.DownloadAndInstallAsync(release, progress);
            _progress = null;
            RenderUpdate();
        }));
        box.Add(Muted(Updates.PlatformName == "PC"
            ? "Tes parties et ton contenu sont gardés. Le jeu se ferme, se met à jour et se relance."
            : "Tes parties et ton contenu sont gardés. La première fois, Android demande d'autoriser l'installation.", 11));
        _updateHost.Content = Card(box, Theme.Highlight, Theme.Gold500);
    }
}
