using ProjetSKE.App.Ui;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Mode Histoire : boîte de dialogue centrée, par-dessus tout.
/// Style « carte d'accès » de Service Impérial : bandeau pierre + filet d'or, corps parchemin.
/// </summary>
public sealed class DialogueView : ContentView
{
    private readonly GameSession _session;
    private readonly DialogueRunner _runner;
    private readonly Action _onEnd;

    public DialogueView(GameSession session, DialogueRunner runner, Action onEnd)
    {
        _session = session;
        _runner = runner;
        _onEnd = onEnd;
        Render();
    }

    private void Render()
    {
        var notifications = _session.Notifications.ToList();
        _session.Notifications.Clear();

        if (_runner.IsFinished && notifications.Count == 0)
        {
            _onEnd();
            return;
        }

        var node = _runner.Current;
        var speaker = node?.Speaker ?? "";
        var narration = speaker.Length == 0;

        // Bandeau sombre : qui parle.
        var band = new VerticalStackLayout
        {
            BackgroundColor = Theme.Stone900,
            Spacing = 0,
            Children =
            {
                GoldLine(4),
                new VerticalStackLayout
                {
                    Padding = new Thickness(20, 18, 20, 16),
                    Spacing = 8,
                    Children =
                    {
                        narration ? Emblem(Ico.Feather, 64) : Avatar(speaker, Theme.AvatarColor(speaker), 64),
                        new Label
                        {
                            Text = (narration ? "Récit" : speaker).ToUpperInvariant(), FontFamily = "serif", FontSize = 20,
                            FontAttributes = FontAttributes.Bold, TextColor = Theme.Stone100, CharacterSpacing = 3,
                            HorizontalTextAlignment = TextAlignment.Center,
                        },
                        new Label
                        {
                            Text = narration ? "NARRATION" : "DIALOGUE", FontSize = 9, FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Stone500, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center,
                        },
                    },
                },
            },
        };

        // Corps parchemin : le texte et les choix.
        var body = new VerticalStackLayout { Padding = new Thickness(20, 18, 20, 20), Spacing = 12, BackgroundColor = Theme.Parchment };
        if (node is not null)
        {
            body.Add(new Label
            {
                Text = narration ? node.Text : $"« {node.Text} »",
                FontFamily = "serif",
                FontSize = 17,
                FontAttributes = narration ? FontAttributes.Italic : FontAttributes.None,
                TextColor = Theme.Stone900,
                HorizontalTextAlignment = TextAlignment.Center,
                LineHeight = 1.25,
            });
        }

        foreach (var n in notifications)
        {
            var note = Card(IconRow(Icon(Ico.CircleCheck, 18, Theme.Green600), Txt(n, 13, Theme.Stone900, bold: true)), Colors.White, Theme.Green600.WithAlpha(0.4f), 10);
            note.Padding = new Thickness(12, 8);
            body.Add(note);
        }

        if (node is not null)
        {
            var choices = _runner.Choices;
            if (choices.Count > 0)
            {
                body.Add(Caps("Votre réponse", 9, Theme.Stone500));
                for (var i = 0; i < choices.Count; i++)
                {
                    var index = i;
                    var choice = Btn(choices[i].Text, () => { _runner.Choose(index); Render(); });
                    choice.MinimumHeightRequest = 50;
                    body.Add(choice);
                }
            }
            else
            {
                body.Add(Primary(node.NextId is null ? "Fermer" : "Suite  ▸", () => { _runner.Continue(); Render(); }));
            }
        }
        else
        {
            body.Add(Primary("Fermer", _onEnd));
        }

        var card = new Border
        {
            Content = new VerticalStackLayout { Spacing = 0, Children = { band, body } },
            BackgroundColor = Theme.Parchment,
            Stroke = Theme.Stone800,
            StrokeThickness = 4,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = 0,
            Margin = new Thickness(18),
            VerticalOptions = LayoutOptions.Center,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 12), Radius = 30, Opacity = 0.6f },
        };

        // La boîte est centrée verticalement sur tout l'écran.
        Content = new Grid { VerticalOptions = LayoutOptions.Fill, Children = { card } };
    }
}
