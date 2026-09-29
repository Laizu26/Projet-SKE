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
    /// <summary>Bulle affichée (une réplique peut en avoir plusieurs : narration puis paroles...).</summary>
    private int _shownStep = -1;
    private int _revealed;
    private IDispatcherTimer? _timer;
    private Label? _textLabel;
    /// <summary>Défilement du texte de la boîte classique (null en cinématique).</summary>
    private ScrollView? _textScroll;
    private bool _showHistory;
    private bool _ended;

    // Couches : voile sombre (animé une fois) et contenu (reconstruit à chaque réplique).
    private readonly BoxView _dim = new() { Color = Colors.Black, Opacity = 0 };
    private readonly ContentView _layer = new() { Opacity = 0 };
    private bool _entered;
    private string? _shownPortrait;

    // Aperçu d'un choix trop long pour son bouton (survol souris ou appui long).
    private readonly Label _peekText = new()
    {
        FontSize = 15, LineHeight = 1.3, TextColor = Theme.Stone100,
        HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap,
    };
    private readonly Label _peekHint = new()
    {
        Text = "TOUCHE ENCORE LE CHOIX POUR LE VALIDER", FontSize = 9, FontAttributes = FontAttributes.Bold, CharacterSpacing = 2,
        TextColor = Color.FromArgb("#CA8A04"), HorizontalTextAlignment = TextAlignment.Center, IsVisible = false,
    };
    private readonly Border _peek;

    public DialogueView(GameSession session, DialogueRunner runner, Action onEnd)
    {
        _session = session;
        _runner = runner;
        // Le choix fait est joué (celui qui parle le dit, ou la narration le raconte) avant la suite.
        _runner.EchoChoices = true;
        _onEnd = onEnd;
        _peek = new Border
        {
            IsVisible = false,
            Opacity = 0,
            InputTransparent = true,
            ZIndex = 50,
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 560,
            Margin = new Thickness(16, 90, 16, 0),
            Padding = new Thickness(18, 14),
            BackgroundColor = Color.FromArgb("#1C1917"),
            Stroke = Color.FromArgb("#CA8A04"),
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.6f, Radius = 18, Offset = new Point(0, 6) },
            Content = new VerticalStackLayout { Spacing = 10, Children = { _peekText, _peekHint } },
        };
        BackgroundColor = Colors.Transparent;
        Content = new Grid { Children = { _dim, _barTop, _barBottom, _layer, _peek } };
        Loaded += async (_, _) =>
        {
            if (_entered) return;
            _entered = true;
            // Classique : l'écran s'assombrit, puis le portrait et la boîte apparaissent.
            // Cinématique : noir complet, les bandes de cinéma glissent, puis le texte au milieu.
            await ApplyStyle(animate: true);
            await _layer.FadeTo(1, Cinematic ? 450u : 200u);
        };
        SizeChanged += (_, _) =>
        {
            var bar = Math.Max(40, Height * 0.11);
            _barTop.HeightRequest = _barBottom.HeightRequest = bar;
            if (!_cinematicShown) { _barTop.TranslationY = -bar; _barBottom.TranslationY = bar; }
        };
        Render();
    }

    // ------------------------------------------------------------------ Classique / cinématique

    /// <summary>Bandes noires façon cinéma (en haut et en bas), avec un filet doré.</summary>
    private readonly Grid _barTop = CinemaBar(top: true);
    private readonly Grid _barBottom = CinemaBar(top: false);
    private bool _cinematicShown;

    private bool Cinematic => _runner.Dialogue.Style == DialogueStyle.Cinematic;

    private static Grid CinemaBar(bool top)
    {
        var bar = new Grid
        {
            VerticalOptions = top ? LayoutOptions.Start : LayoutOptions.End,
            HeightRequest = 80,
            InputTransparent = true,
            Children = { new BoxView { Color = Colors.Black } },
        };
        bar.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#A16207"),
            VerticalOptions = top ? LayoutOptions.End : LayoutOptions.Start,
        });
        bar.TranslationY = top ? -200 : 200;
        return bar;
    }

    /// <summary>Met l'écran dans le style du dialogue en cours (il peut changer si l'histoire saute dans un autre dialogue).</summary>
    private async Task ApplyStyle(bool animate)
    {
        var cinematic = Cinematic;
        if (cinematic == _cinematicShown && _entered && !animate) return;
        _cinematicShown = cinematic;
        var bar = Math.Max(40, Height > 0 ? Height * 0.11 : 80);
        uint ms = animate ? 600u : 0u;
        if (cinematic)
        {
            await Task.WhenAll(
                _dim.FadeTo(1, animate ? 500u : 0u, Easing.SinOut),
                _barTop.TranslateTo(0, 0, ms, Easing.CubicOut),
                _barBottom.TranslateTo(0, 0, ms, Easing.CubicOut));
        }
        else
        {
            await Task.WhenAll(
                _dim.FadeTo(0.72, animate ? 280u : 0u, Easing.SinOut),
                _barTop.TranslateTo(0, -bar, ms, Easing.CubicIn),
                _barBottom.TranslateTo(0, bar, ms, Easing.CubicIn));
        }
    }

    /// <summary>Bouton de choix : si son texte est coupé, il s'affiche en entier au survol ou en restant appuyé.</summary>
    /// <summary>
    /// Bouton de choix. Si son texte est coupé : un premier toucher (ou clic) l'affiche en gros au-dessus,
    /// un second le valide. Sur PC, le survol de la souris l'affiche aussi (sans rien valider).
    /// </summary>
    private Button ChoiceButton(string label, string fullText, Action choose, bool enabled)
    {
        Button? button = null;
        button = Btn(label, () =>
        {
            if (_armedChoice != button && IsCut(button!))
            {
                // Le choix touché est entouré d'or ; l'ancien reprend son allure.
                if (_armedChoice is { } previous) { previous.BorderColor = _armedBorder; previous.BorderWidth = 1; }
                _armedChoice = button;
                _armedBorder = button.BorderColor;
                button.BorderColor = Color.FromArgb("#EAB308");
                button.BorderWidth = 2.5;
                ShowPeek(fullText, big: true);
                return;
            }
            _armedChoice = null;
            ShowPeek(null);
            choose();
        }, enabled: enabled);
        Interactive.AttachHover(button, () => IsCut(button) && _armedChoice is null, on => ShowPeek(on ? fullText : null));
        return button;
    }

    /// <summary>Choix touché une fois (affiché en gros) : le toucher encore le valide.</summary>
    private Button? _armedChoice;
    private Color? _armedBorder;

    /// <summary>Le texte (en capitales espacées) dépasse-t-il de la largeur du bouton ? Estimation prudente.</summary>
    private static bool IsCut(Button button)
    {
        var text = button.Text ?? "";
        var room = button.Width - button.Padding.HorizontalThickness - 8;
        if (room <= 0) return text.Length > 30;
        var perChar = button.FontSize * 0.7 + button.CharacterSpacing * button.FontSize / 16;
        return text.Length * perChar > room * 0.92;
    }

    /// <summary>
    /// Affiche (texte) ou cache (null) l'aperçu du choix, en haut de l'écran.
    /// En gros (<paramref name="big"/>) après un premier toucher, avec l'indication pour valider.
    /// </summary>
    public void ShowPeek(string? text, bool big = false)
    {
        _peek.AbortAnimation("FadeTo");
        if (text is null)
        {
            _peek.IsVisible = false;
            _peek.Opacity = 0;
            return;
        }
        _peekText.Text = text;
        _peekText.FontSize = big ? 22 : 15;
        _peekText.FontFamily = big ? "serif" : null;
        _peekHint.IsVisible = big;
        _peek.Margin = new Thickness(16, Math.Max(40, Height * 0.11) + 24, 16, 0);
        _peek.IsVisible = true;
        _ = _peek.FadeTo(1, 140);
    }

    /// <summary>Test automatique : montre l'aperçu du premier choix affiché.</summary>
    public bool PeekFirstChoice()
    {
        var options = _runner.Options;
        if (options.Count == 0) return false;
        ShowPeek(options[0].Text, big: true);
        return true;
    }

    /// <summary>
    /// Cinématique : plus d'interface, tout est au milieu de l'écran noir — l'image, le nom, le texte, puis les choix.
    /// Toucher n'importe où fait avancer.
    /// </summary>
    private View CinematicStage(DialogueNode? node)
    {
        var center = new VerticalStackLayout
        {
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(28, 0),
            MaximumWidthRequest = 620,
        };
        if (_notes.Count > 0)
            foreach (var n in _notes)
                center.Add(IconRow(Icon(Ico.Sparkles, 13, Theme.Gold500), Txt(n, 13, Theme.Gold400, bold: true)));

        if (node is not null)
        {
            if (_runner.Portrait is { } portrait)
            {
                var narration = Narration;
                var frame = new Border
                {
                    WidthRequest = narration ? 320 : 170,
                    HeightRequest = narration ? 200 : 210,
                    HorizontalOptions = LayoutOptions.Center,
                    Stroke = Color.FromArgb("#A16207"),
                    StrokeThickness = 1.5,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    BackgroundColor = Colors.Black,
                    Content = new FramedImage(portrait),
                };
                if (_shownPortrait != portrait.Id)
                {
                    _shownPortrait = portrait.Id;
                    frame.Opacity = 0;
                    frame.Loaded += async (_, _) => await frame.FadeTo(1, 500, Easing.SinOut);
                }
                center.Add(frame);
            }
            else _shownPortrait = null;

            if (_runner.Speaker.Length > 0)
            {
                var name = new Label
                {
                    Text = _runner.Speaker.ToUpperInvariant(), FontFamily = "serif", FontSize = 13, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Gold500, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center,
                };
                center.Add(name);
            }
            var full = FullText();
            _textLabel = new Label
            {
                Text = full[..Math.Min(_revealed, full.Length)],
                FontFamily = "serif",
                FontSize = 21,
                LineHeight = 1.4,
                TextColor = Theme.Stone100,
                FontAttributes = Narration ? FontAttributes.Italic : FontAttributes.None,
                HorizontalTextAlignment = TextAlignment.Center,
                MinimumHeightRequest = 60,
            };
            // Texte long : il défile au lieu de sortir de l'écran.
            _textScroll = new ScrollView
            {
                Content = _textLabel,
                MaximumHeightRequest = Math.Max(180, (Height > 0 ? Height : 800) * 0.45),
            };
            center.Add(_textScroll);

            var options = _runner.Options;
            if (!Typing && options.Count > 0)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    var index = i;
                    var option = options[i];
                    var choice = ChoiceButton((option.Enabled ? (option.Choice.Narration ? "✦  " : "›  ") : "✕  ") + option.Text, option.Text, () =>
                    {
                        _runner.ChooseOption(index);
                        Render();
                    }, option.Enabled);
                    choice.BackgroundColor = Color.FromArgb("#1C1917");
                    choice.BorderColor = Color.FromArgb("#A16207");
                    choice.TextColor = Theme.Stone100;
                    choice.MinimumHeightRequest = 48;
                    if (option.Choice.Narration) choice.FontAttributes = FontAttributes.Italic;
                    center.Add(choice);
                    if (!option.Enabled && option.LockedText.Length > 0)
                        center.Add(IconRow(Icon(Ico.Lock, 11, Theme.Stone500), Txt(option.LockedText, 11, Theme.Stone400)));
                }
            }
            else
            {
                var hint = Caps(Typing ? "Toucher pour tout afficher" : !_runner.HasMoreSegments && node.NextId is null && node.Branches.Count == 0 ? "Toucher pour terminer" : "Toucher pour continuer", 8, Theme.Stone500);
                hint.HorizontalTextAlignment = TextAlignment.Center;
                if (!Typing) Blink(hint);
                center.Add(hint);
            }
        }
        else center.Add(Primary("Fermer", End));

        var stage = new Grid { Children = { center } };
        if (_showHistory) stage.Add(HistoryPanel());
        // Toucher n'importe où avance (sans l'éclat doré des boutons : c'est tout l'écran).
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OnBoxTapped();
        stage.GestureRecognizers.Add(tap);
        stage.BackgroundColor = Colors.Transparent;
        return stage;
    }

    /// <summary>Texte de la réplique en cours, tel que joué (variante choisie, balises remplacées).</summary>
    private string FullText() => _runner.Text.Length == 0 ? "" : _runner.Speaker.Length > 0 ? $"« {_runner.Text} »" : _runner.Text;
    /// <summary>Narration : le récit, sans personnage (texte centré sur un bandeau sombre, sans nom ni portrait).</summary>
    private bool Narration => _runner.Current is not null && _runner.Speaker.Length == 0;
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
        _armedChoice = null;
        ShowPeek(null);
        _notes.AddRange(_session.Notifications);
        _session.Notifications.Clear();

        var node = _runner.Current;
        if (node is null && _notes.Count == 0)
        {
            End();
            return;
        }

        // Nouvelle réplique : on l'ajoute à l'historique et on relance la machine à écrire.
        if (node is not null && _runner.Step != _shownStep)
        {
            _shownNode = node;
            _shownStep = _runner.Step;
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
        if (_entered && Cinematic != _cinematicShown) _ = ApplyStyle(animate: true);
        if (Cinematic)
        {
            // Les commandes (historique, passer) restent discrètes, entre les bandes noires.
            var top = TopBar();
            top.Opacity = 0.55;
            top.Margin = new Thickness(0, Math.Max(40, Height * 0.11), 0, 0);
            grid.Add(top, 0, 0);
            var stage = CinematicStage(node);
            grid.Add(stage, 0, 1);
            Grid.SetRowSpan(stage, 3);
            _layer.Content = grid;
            return;
        }
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
        var narration = Narration;
        var frame = new Border
        {
            // Narration : une illustration centrée (paysage) ; réplique : le portrait de celui qui parle, à gauche.
            WidthRequest = narration ? 290 : 170,
            HeightRequest = narration ? 180 : 210,
            HorizontalOptions = narration ? LayoutOptions.Center : LayoutOptions.Start,
            Margin = narration ? new Thickness(0, 0, 0, -8) : new Thickness(22, 0, 0, -26),
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
        // Le nom du dialogue est un nom de travail (éditeur) : seul le « titre affiché en jeu » est montré au joueur.
        var chapter = _session.FormatText(_runner.Dialogue.DisplayTitle).Trim();
        var bar = new Grid
        {
            Padding = new Thickness(16, 14, 12, 12),
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
        };
        var titles = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children = { IconCaps(Ico.BookOpen, _session.Db.T("title.story"), Theme.Gold500, 9) },
        };
        if (chapter.Length > 0)
            titles.Add(new Label { Text = chapter.ToUpperInvariant(), FontFamily = "serif", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Theme.Stone100, CharacterSpacing = 2 });
        bar.Add(titles, 0, 0);
        // Icônes seules (même taille qu'avant) : l'historique (une croix quand il est ouvert) et passer.
        bar.Add(DarkPill(_showHistory ? Ico.X : Ico.ScrollText, () => { _showHistory = !_showHistory; Render(); }), 1, 0);
        bar.Add(DarkPill(Ico.SkipForward, Skip), 2, 0);
        return bar;
    }

    private static View DarkPill(string glyph, Action onTap)
    {
        var pill = new Border
        {
            BackgroundColor = Theme.Stone800,
            Stroke = Theme.Stone700,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(10, 7),
            VerticalOptions = LayoutOptions.Center,
            Content = Icon(glyph, 12, Theme.Gold500),
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
            var narration = Narration;
            _textLabel = new Label
            {
                Text = full[..Math.Min(_revealed, full.Length)],
                FontFamily = "serif",
                FontSize = narration ? 18 : 17,
                LineHeight = narration ? 1.35 : 1.25,
                TextColor = narration ? Theme.Stone100 : Theme.Stone900,
                FontAttributes = narration ? FontAttributes.Italic : FontAttributes.None,
                HorizontalTextAlignment = narration ? TextAlignment.Center : TextAlignment.Start,
                MinimumHeightRequest = 70,
            };
            if (narration)
            {
                // Ornement : un filet doré de part et d'autre d'une petite plume.
                var ornament = new Grid
                {
                    ColumnSpacing = 10,
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                };
                ornament.Add(new BoxView { HeightRequest = 1, Color = Theme.Gold700, VerticalOptions = LayoutOptions.Center }, 0, 0);
                ornament.Add(Icon(Ico.Feather, 12, Theme.Gold500), 1, 0);
                ornament.Add(new BoxView { HeightRequest = 1, Color = Theme.Gold700, VerticalOptions = LayoutOptions.Center }, 2, 0);
                body.Add(ornament);
            }
            // Texte long : il défile dans la boîte au lieu de déborder sous les choix (et suit la machine à écrire).
            _textScroll = new ScrollView
            {
                Content = _textLabel,
                MaximumHeightRequest = Math.Max(160, (Height > 0 ? Height : 800) * 0.4),
            };
            body.Add(_textScroll);

            var options = _runner.Options;
            if (!Typing && options.Count > 0)
            {
                body.Add(Caps("Votre réponse", 9, Theme.Stone500));
                for (var i = 0; i < options.Count; i++)
                {
                    var index = i;
                    var option = options[i];
                    var narrative = option.Choice.Narration;
                    var marker = !option.Enabled ? "✕  " : narrative ? "✦  " : "›  ";
                    var choice = ChoiceButton(marker + option.Text, option.Text, () =>
                    {
                        // Le choix est ensuite joué comme une réplique (il rejoint l'historique à ce moment-là).
                        _runner.ChooseOption(index);
                        Render();
                    }, option.Enabled);
                    choice.MinimumHeightRequest = 48;
                    if (narrative)
                    {
                        // Choix-narration : une action, pas une parole (italique, sur fond sombre).
                        choice.FontAttributes = FontAttributes.Italic;
                        choice.BackgroundColor = Theme.Stone800;
                        choice.BorderColor = Theme.Gold700;
                        choice.TextColor = Theme.Stone100;
                    }
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
                        Caps(Typing ? "Toucher pour tout afficher" : !_runner.HasMoreSegments && node.NextId is null && node.Branches.Count == 0 ? "Toucher pour terminer" : "Toucher pour continuer", 8, Narration ? Theme.Stone400 : Theme.Stone500),
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

        var narrationBox = node is not null && Narration;
        var box = new Border
        {
            BackgroundColor = narrationBox ? Color.FromArgb("#F20C0A09") : Theme.Parchment,
            Stroke = narrationBox ? Theme.Gold700 : Theme.Stone800,
            StrokeThickness = narrationBox ? 1.5 : 3,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = 0,
            Margin = new Thickness(12, 18, 12, 16),
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, -4), Radius = 20, Opacity = 0.5f },
            Content = body,
        };
        OnTap(box, OnBoxTapped);

        // Plaque du nom, qui déborde sur le haut de la boîte (pas pour la narration : personne ne parle).
        var container = new Grid();
        container.Add(box);
        if (node is not null && !narrationBox)
        {
            var speaker = _runner.Speaker;
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
        var cps = TextSpeed.CharsPerSecond;
        if (cps <= 0)
        {
            _revealed = FullText().Length; // vitesse « Instantanée »
            return;
        }
        var pause = 0;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / cps);
        _timer.Tick += (_, _) =>
        {
            if (_shownNode is null || _ended) { _timer?.Stop(); return; }
            if (pause > 0) { pause--; return; }
            var full = FullText();
            _revealed = Math.Min(full.Length, _revealed + 1);
            // Petite respiration après la ponctuation, comme à l'oral.
            var last = _revealed > 0 ? full[_revealed - 1] : ' ';
            if (_revealed < full.Length) pause = last is '.' or '!' or '?' or '…' ? 7 : last is ',' or ';' or ':' ? 3 : 0;
            if (_textLabel is not null) _textLabel.Text = full[.._revealed];
            // Texte plus long que la boîte : on suit la dernière ligne (de temps en temps, pas à chaque lettre).
            if (_textScroll is { } scroll && _revealed % 24 == 0 && scroll.ContentSize.Height > scroll.Height + 4)
                _ = scroll.ScrollToAsync(0, scroll.ContentSize.Height, false);
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
        for (var guard = 0; guard < 400 && _runner.Current is { } node && !_runner.HasOptions; guard++)
        {
            if (_runner.Step != _shownStep) _history.Add((_runner.Speaker, _runner.Text));
            _shownNode = node;
            _shownStep = _runner.Step;
            _runner.Continue();
        }
        if (_runner.Current is { } next)
        {
            if (_runner.Step != _shownStep) _history.Add((_runner.Speaker, _runner.Text));
            _shownNode = next;
            _shownStep = _runner.Step;
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
