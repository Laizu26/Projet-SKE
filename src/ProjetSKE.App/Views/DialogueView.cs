using Microsoft.Maui.Controls.Shapes;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Mode Histoire, par-dessus le jeu : l'écran s'assombrit, le portrait de celui qui parle apparaît
/// (s'il en a un), puis la boîte de dialogue monte (effet machine à écrire) avec les choix.
/// En haut : le chapitre et les commandes (historique, passer).
/// Toucher la boîte : affiche tout le texte, puis passe à la suite.
/// </summary>
public sealed class DialogueView : ContentView
{
    private readonly GameSession _session;
    private readonly DialogueRunner _runner;
    private readonly Action _onEnd;

    private readonly List<(string Speaker, string Text)> _history = [];
    private readonly List<string> _notes = [];
    private DialogueNode? _shownNode;
    private int _revealed;
    private IDispatcherTimer? _timer;
    private Label? _textLabel;
    private bool _showHistory;
    private bool _ended;

    // Couches : voile sombre (animé une fois) et contenu (reconstruit à chaque réplique).
    private readonly BoxView _dim = new() { Color = Colors.Black, Opacity = 0 };
    private readonly ContentView _layer = new() { Opacity = 0 };
    private bool _entered;
    private string? _shownPortrait;

    public DialogueView(GameSession session, DialogueRunner runner, Action onEnd)
    {
        _session = session;
        _runner = runner;
        _onEnd = onEnd;
        BackgroundColor = Colors.Transparent;
        Content = new Grid { Children = { _dim, _layer } };
        Loaded += async (_, _) =>
        {
            if (_entered) return;
            _entered = true;
            // 1. L'écran s'assombrit, 2. le portrait et la boîte apparaissent.
            await _dim.FadeTo(0.72, 280, Easing.SinOut);
            await _layer.FadeTo(1, 200);
        };
        Render();
    }

    /// <summary>Texte de la réplique en cours, tel que joué (variante choisie, balises remplacées).</summary>
    private string FullText() => _runner.Speaker.Length > 0 ? $"« {_runner.Text} »" : _runner.Text;
    private bool Typing => _shownNode is not null && _revealed < FullText().Length;

    private void End()
    {
        if (_ended) return;
        _ended = true;
        _timer?.Stop();
        _onEnd();
    }

    private void Render()
    {
        if (_ended) return;
        _notes.AddRange(_session.Notifications);
        _session.Notifications.Clear();

        var node = _runner.Current;
        if (node is null && _notes.Count == 0)
        {
            End();
            return;
        }

        // Nouvelle réplique : on l'ajoute à l'historique et on relance la machine à écrire.
        if (node is not null && !ReferenceEquals(node, _shownNode))
        {
            _shownNode = node;
            _revealed = 0;
            _history.Add((_runner.Speaker, _runner.Text));
            StartTyping();
        }

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };
        grid.Add(TopBar(), 0, 0);
        grid.Add(Stage(), 0, 1);
        grid.Add(PortraitView(), 0, 2);
        grid.Add(TextBox(node), 0, 3);
        _layer.Content = grid;
    }

    // ------------------------------------------------------------------ Portrait

    /// <summary>Portrait de celui qui parle, au-dessus de la boîte ; il apparaît en glissant quand l'orateur change.</summary>
    private View PortraitView()
    {
        var portrait = _runner.Portrait;
        if (portrait is null)
        {
            _shownPortrait = null;
            return new BoxView { HeightRequest = 0, Color = Colors.Transparent };
        }
        var frame = new Border
        {
            WidthRequest = 170,
            HeightRequest = 210,
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(22, 0, 0, -26),
            Stroke = Theme.Gold600,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            BackgroundColor = Theme.Stone900,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 6), Radius = 18, Opacity = 0.6f },
            Content = new FramedImage(portrait),
        };
        if (_shownPortrait != portrait.Id)
        {
            _shownPortrait = portrait.Id;
            frame.Opacity = 0;
            frame.TranslationY = 24;
            frame.Loaded += async (_, _) =>
                await Task.WhenAll(frame.FadeTo(1, 260, Easing.SinOut), frame.TranslateTo(0, 0, 260, Easing.CubicOut));
        }
        return frame;
    }

    // ------------------------------------------------------------------ Barre du haut

    private View TopBar()
    {
        var chapter = _runner.Dialogue.Name.Length > 0 ? _runner.Dialogue.Name : _session.Db.T("title.narration");
        var bar = new Grid
        {
            Padding = new Thickness(16, 14, 12, 12),
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
        };
        bar.Add(new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                IconCaps(Ico.BookOpen, _session.Db.T("title.story"), Theme.Gold500, 9),
                new Label { Text = chapter.ToUpperInvariant(), FontFamily = "serif", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Theme.Stone100, CharacterSpacing = 2 },
            },
        }, 0, 0);
        bar.Add(DarkPill(Ico.ScrollText, _showHistory ? "Fermer" : "Historique", () => { _showHistory = !_showHistory; Render(); }), 1, 0);
        bar.Add(DarkPill(Ico.SkipForward, "Passer", Skip), 2, 0);
        return bar;
    }

    private static View DarkPill(string glyph, string text, Action onTap)
    {
        var pill = new Border
        {
            BackgroundColor = Theme.Stone800,
            Stroke = Theme.Stone700,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(10, 7),
            VerticalOptions = LayoutOptions.Center,
            Content = new HorizontalStackLayout
            {
                Spacing = 5,
                Children = { Icon(glyph, 12, Theme.Gold500), Caps(text, 8, Theme.Stone300) },
            },
        };
        return OnTap(pill, onTap);
    }

    // ------------------------------------------------------------------ La scène

    private View Stage()
    {
        var stage = new Grid();
        var place = Caps(_session.CurrentLocation.Name, 9, Theme.Stone400);
        place.HorizontalOptions = LayoutOptions.Center;
        place.VerticalOptions = LayoutOptions.Start;
        place.Margin = new Thickness(0, 6, 0, 0);
        stage.Add(place);

        // Messages de l'histoire (recrutement, quête, objet...).
        if (_notes.Count > 0)
        {
            var notes = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.End, Margin = new Thickness(16, 0, 16, 10) };
            foreach (var n in _notes)
            {
                notes.Add(new Border
                {
                    BackgroundColor = Theme.Stone900,
                    Stroke = Theme.Gold600,
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(12, 8),
                    Content = IconRow(Icon(Ico.Sparkles, 14, Theme.Gold500), Txt(n, 13, Theme.Stone100, bold: true)),
                });
            }
            stage.Add(notes);
        }

        if (_showHistory) stage.Add(HistoryPanel());
        return stage;
    }

    private View HistoryPanel()
    {
        var list = new VerticalStackLayout { Spacing = 10 };
        foreach (var (speaker, text) in _history)
        {
            list.Add(new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    Caps(speaker.Length > 0 ? speaker : _session.Db.T("title.narration"), 9, speaker.Length > 0 ? Theme.Gold700 : Theme.Stone400),
                    new Label
                    {
                        Text = text, FontFamily = "serif", FontSize = 14, TextColor = Theme.Stone900,
                        FontAttributes = speaker.Length > 0 ? FontAttributes.None : FontAttributes.Italic,
                    },
                },
            });
        }
        var scroll = new ScrollView { Content = list };
        scroll.Loaded += async (_, _) => await scroll.ScrollToAsync(0, 100000, false);
        return new Border
        {
            BackgroundColor = Theme.ParchmentLight,
            Stroke = Theme.Stone800,
            StrokeThickness = 3,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(16, 12),
            Margin = new Thickness(14, 8),
            Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
                RowSpacing = 8,
                Children = { IconCaps(Ico.ScrollText, "Historique du dialogue", Theme.Stone500, 9) },
            }.WithRow1(scroll),
        };
    }

    // ------------------------------------------------------------------ Boîte de texte

    private View TextBox(DialogueNode? node)
    {
        var body = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(20, 26, 20, 18) };

        if (node is not null)
        {
            var full = FullText();
            _textLabel = new Label
            {
                Text = full[..Math.Min(_revealed, full.Length)],
                FontFamily = "serif",
                FontSize = 17,
                LineHeight = 1.25,
                TextColor = Theme.Stone900,
                FontAttributes = _runner.Speaker.Length > 0 ? FontAttributes.None : FontAttributes.Italic,
                MinimumHeightRequest = 70,
            };
            body.Add(_textLabel);

            var options = _runner.Options;
            if (!Typing && options.Count > 0)
            {
                body.Add(Caps("Votre réponse", 9, Theme.Stone500));
                for (var i = 0; i < options.Count; i++)
                {
                    var index = i;
                    var option = options[i];
                    var choice = Btn((option.Enabled ? "›  " : "✕  ") + option.Text, () => { _runner.ChooseOption(index); Render(); }, enabled: option.Enabled);
                    choice.MinimumHeightRequest = 48;
                    body.Add(choice);
                    if (!option.Enabled && option.LockedText.Length > 0)
                        body.Add(IconRow(Icon(Ico.Lock, 11, Theme.Stone500), Txt(option.LockedText, 11, Theme.Stone500)));
                }
            }
            else
            {
                var hint = new HorizontalStackLayout
                {
                    Spacing = 6,
                    HorizontalOptions = LayoutOptions.End,
                    Children =
                    {
                        Caps(Typing ? "Toucher pour tout afficher" : node.NextId is null && node.Branches.Count == 0 ? "Toucher pour terminer" : "Toucher pour continuer", 8, Theme.Stone500),
                        Icon(Ico.ChevronRight, 14, Theme.Gold600),
                    },
                };
                if (!Typing) Blink(hint);
                body.Add(hint);
            }
        }
        else
        {
            body.Add(Primary("Fermer", End));
        }

        var box = new Border
        {
            BackgroundColor = Theme.Parchment,
            Stroke = Theme.Stone800,
            StrokeThickness = 3,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = 0,
            Margin = new Thickness(12, 18, 12, 16),
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, -4), Radius = 20, Opacity = 0.5f },
            Content = body,
        };
        OnTap(box, OnBoxTapped);

        // Plaque du nom, qui déborde sur le haut de la boîte.
        var container = new Grid();
        container.Add(box);
        if (node is not null)
        {
            var speaker = _runner.Speaker.Length > 0 ? _runner.Speaker : _session.Db.T("title.narration");
            var plate = new Border
            {
                BackgroundColor = Theme.Stone900,
                Stroke = Theme.Gold600,
                StrokeThickness = 1.5,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(14, 6),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(28, 4, 0, 0),
                InputTransparent = true,
                Content = new Label
                {
                    Text = speaker.ToUpperInvariant(), FontFamily = "serif", FontSize = 13, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Gold500, CharacterSpacing = 2,
                },
            };
            container.Add(plate);
        }
        return container;
    }

    private static void Blink(VisualElement element)
    {
        var animation = new Animation
        {
            { 0, 0.5, new Animation(v => element.Opacity = v, 1, 0.35) },
            { 0.5, 1, new Animation(v => element.Opacity = v, 0.35, 1) },
        };
        element.Loaded += (_, _) => animation.Commit(element, "blink", 16, 1400, repeat: () => element.IsLoaded);
    }

    // ------------------------------------------------------------------ Déroulement

    private void StartTyping()
    {
        _timer?.Stop();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(22);
        _timer.Tick += (_, _) =>
        {
            if (_shownNode is null || _ended) { _timer?.Stop(); return; }
            var full = FullText();
            _revealed = Math.Min(full.Length, _revealed + 2);
            if (_textLabel is not null) _textLabel.Text = full[.._revealed];
            if (_revealed >= full.Length)
            {
                _timer?.Stop();
                Render(); // affiche les choix / l'indication de suite
            }
        };
        _timer.Start();
    }

    private void OnBoxTapped()
    {
        if (_shownNode is null) return;
        if (Typing)
        {
            _timer?.Stop();
            _revealed = FullText().Length;
            Render();
            return;
        }
        if (_runner.HasOptions) return; // il faut choisir une réponse
        _notes.Clear();
        _runner.Continue();
        Render();
    }

    /// <summary>Avance jusqu'au prochain choix (ou la fin), en gardant tout dans l'historique.</summary>
    private void Skip()
    {
        _timer?.Stop();
        for (var guard = 0; guard < 200 && _runner.Current is { } node && !_runner.HasOptions; guard++)
        {
            if (!ReferenceEquals(node, _shownNode)) _history.Add((_runner.Speaker, _runner.Text));
            _shownNode = node;
            _runner.Continue();
        }
        if (_runner.Current is { } next)
        {
            if (!ReferenceEquals(next, _shownNode)) _history.Add((_runner.Speaker, _runner.Text));
            _shownNode = next;
            _revealed = FullText().Length;
        }
        Render();
    }
}

internal static class GridExtensions
{
    /// <summary>Ajoute un élément en deuxième ligne d'une grille (petit raccourci d'écriture).</summary>
    public static Grid WithRow1(this Grid grid, View view)
    {
        grid.Add(view, 0, 1);
        return grid;
    }
}
