using ProjetSKE.App.Ui;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Mode Histoire : boîte de dialogue centrée, par-dessus tout, avec choix de réponse.</summary>
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

        var box = new VerticalStackLayout { Spacing = 14 };

        if (_runner.Current is { } node)
        {
            if (node.Speaker.Length > 0)
            {
                box.Add(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        Avatar(node.Speaker, Theme.AvatarColor(node.Speaker), 72),
                        new Label
                        {
                            Text = node.Speaker, FontSize = 20, FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.AccentLight, HorizontalTextAlignment = TextAlignment.Center, CharacterSpacing = 1,
                        },
                    },
                });
                box.Add(new Label
                {
                    Text = node.Text, FontSize = 18, TextColor = Theme.Text,
                    HorizontalTextAlignment = TextAlignment.Center, LineHeight = 1.2,
                });
            }
            else
            {
                box.Add(new Label { Text = "✦", FontSize = 22, TextColor = Theme.Accent, HorizontalTextAlignment = TextAlignment.Center });
                box.Add(new Label
                {
                    Text = node.Text, FontSize = 18, TextColor = Theme.Text, FontAttributes = FontAttributes.Italic,
                    HorizontalTextAlignment = TextAlignment.Center, LineHeight = 1.2,
                });
            }
        }

        foreach (var n in notifications)
        {
            var note = Card(Txt("✔ " + n, 14, Theme.Good, bold: true), Theme.Surface2, Theme.Good.WithAlpha(0.5f), 12);
            note.Padding = new Thickness(12, 8);
            box.Add(note);
        }

        if (_runner.Current is { } current)
        {
            var choices = _runner.Choices;
            if (choices.Count > 0)
            {
                box.Add(new BoxView { HeightRequest = 1, Color = Theme.Stroke, Margin = new Thickness(0, 4) });
                for (var i = 0; i < choices.Count; i++)
                {
                    var index = i;
                    var choice = Btn("›  " + choices[i].Text, () => { _runner.Choose(index); Render(); });
                    choice.MinimumHeightRequest = 50;
                    box.Add(choice);
                }
            }
            else
            {
                box.Add(Primary(current.NextId is null ? "Fermer" : "Suite  ▸", () => { _runner.Continue(); Render(); }));
            }
        }
        else
        {
            box.Add(Primary("Fermer", _onEnd));
        }

        var card = new Border
        {
            Content = box,
            Background = Theme.Vertical(Theme.Surface2, Theme.Surface),
            Stroke = Theme.Accent.WithAlpha(0.6f),
            StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Padding = new Thickness(22, 24),
            Margin = new Thickness(18),
            VerticalOptions = LayoutOptions.Center,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 10), Radius = 30, Opacity = 0.7f },
        };

        // La boîte est centrée verticalement sur tout l'écran.
        Content = new Grid { VerticalOptions = LayoutOptions.Fill, Children = { card } };
    }
}
