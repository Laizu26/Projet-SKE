using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Onglet Quêtes : quêtes en cours (objectif actuel) et quêtes terminées.</summary>
public sealed class QuestsView : ContentView
{
    public QuestsView(GamePage page)
    {
        var s = page.Session;
        var log = s.QuestLog.ToList();
        var active = log.Where(q => q.Progress.Status == QuestStatus.Active).ToList();
        var done = log.Where(q => q.Progress.Status == QuestStatus.Completed).ToList();

        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Add(ButtonRow(
            Btn($"📜  En cours ({active.Count})", () => { page.QuestsShowDone = false; page.Render(); }, selected: !page.QuestsShowDone),
            Btn($"🏆  Terminées ({done.Count})", () => { page.QuestsShowDone = true; page.Render(); }, selected: page.QuestsShowDone)));

        var list = page.QuestsShowDone ? done : active;
        if (list.Count == 0)
        {
            stack.Add(Card(new VerticalStackLayout
            {
                Spacing = 8,
                Padding = new Thickness(0, 30),
                Children =
                {
                    Icon(page.QuestsShowDone ? "🏆" : "📜", 48),
                    new Label
                    {
                        Text = page.QuestsShowDone ? "Aucune quête terminée pour l'instant." : "Aucune quête en cours.\nParle aux habitants des villes !",
                        FontSize = 15, TextColor = Theme.Muted, HorizontalTextAlignment = TextAlignment.Center,
                    },
                },
            }));
        }

        foreach (var (quest, progress) in list)
        {
            var completed = progress.Status == QuestStatus.Completed;
            var body = new VerticalStackLayout { Spacing = 8 };
            body.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    Txt(quest.Name, 18, completed ? Theme.Good : Theme.AccentLight, bold: true),
                    Badge(completed ? "Terminée" : $"Étape {Math.Min(progress.Step + 1, quest.Objectives.Count)}/{quest.Objectives.Count}", completed ? Theme.Good : Theme.Accent),
                },
            });
            body.Add(Txt(quest.Description, 14, Theme.Muted));

            for (var i = 0; i < quest.Objectives.Count; i++)
            {
                var o = quest.Objectives[i];
                var text = s.ObjectiveText(o);
                if (i < progress.Step)
                {
                    body.Add(Txt("✔  " + text, 14, Theme.Good));
                }
                else if (i == progress.Step && !completed)
                {
                    var objective = Stack(Txt("▶  " + text, 15, Theme.Text, bold: true));
                    if (o.Type == ObjectiveType.Defeat && o.Count > 1)
                        objective.Add(Bar("", progress.Count, o.Count, Theme.Accent, 8));
                    var current = Card(objective, Theme.Surface2, Theme.Accent.WithAlpha(0.5f), 12);
                    current.Padding = new Thickness(12, 10);
                    body.Add(current);
                }
                // Les objectifs suivants restent cachés : on découvre la suite en avançant.
            }

            var card = Card(body, stroke: completed ? Theme.Good.WithAlpha(0.4f) : Theme.Accent.WithAlpha(0.4f));
            stack.Add(card);
        }
        Content = stack;
    }
}
