using ProjetSKE.App.Ui;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Mode Histoire : boîte de dialogue par-dessus tout, avec choix de réponse.</summary>
public sealed class DialogueView : ContentView
{
    private readonly DialogueRunner _runner;
    private readonly Action _onEnd;

    public DialogueView(DialogueRunner runner, Action onEnd)
    {
        _runner = runner;
        _onEnd = onEnd;
        Render();
    }

    private void Render()
    {
        var notifications = _runner.Notifications.ToList();
        _runner.Notifications.Clear();

        if (_runner.IsFinished && notifications.Count == 0)
        {
            _onEnd();
            return;
        }

        var box = Stack();
        foreach (var n in notifications) box.Add(Txt(n, 14, Theme.Good, bold: true));

        if (_runner.Current is { } node)
        {
            if (node.Speaker.Length > 0) box.Add(Txt(node.Speaker, 16, Theme.Accent, bold: true));
            box.Add(Txt(node.Text, 16, node.Speaker.Length > 0 ? Theme.Text : Theme.Muted));

            if (node.Choices.Count > 0)
            {
                for (var i = 0; i < node.Choices.Count; i++)
                {
                    var index = i;
                    box.Add(Btn(node.Choices[i].Text, () => { _runner.Choose(index); Render(); }));
                }
            }
            else
            {
                box.Add(Btn(node.NextId is null ? "Fermer" : "Suite ▸", () => { _runner.Continue(); Render(); }));
            }
        }
        else
        {
            box.Add(Btn("Fermer", _onEnd));
        }

        Content = new Grid
        {
            Padding = new Thickness(12),
            Children = { new ContentView { Content = Panel(box), VerticalOptions = LayoutOptions.End } },
        };
    }
}
