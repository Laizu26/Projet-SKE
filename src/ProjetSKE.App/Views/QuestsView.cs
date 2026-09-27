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

        var stack = new VerticalStackLayout { Spacing = 14 };
        stack.Add(ButtonRow(
            Btn($"En cours ({active.Count})", () => { page.QuestsShowDone = false; page.Render(); }, selected: !page.QuestsShowDone),
            Btn($"Terminées ({done.Count})", () => { page.QuestsShowDone = true; page.Render(); }, selected: page.QuestsShowDone)));

        var list = page.QuestsShowDone ? done : active;
        if (list.Count == 0)
        {
            stack.Add(Card(new VerticalStackLayout
            {
                Spacing = 10,
                Padding = new Thickness(0, 30),
                Children =
                {
                    Icon(page.QuestsShowDone ? Ico.Trophy : Ico.Scroll, 40, Theme.Stone300),
                    new Label
                    {
                        Text = page.QuestsShowDone ? "Aucune quête accomplie pour l'instant." : "Aucune quête en cours.\nParle aux habitants des villes.",
                        FontSize = 14, TextColor = Theme.Stone500, HorizontalTextAlignment = TextAlignment.Center,
                    },
                },
            }));
        }

        foreach (var (quest, progress) in list)
        {
            var completed = progress.Status == QuestStatus.Completed;
            var body = new VerticalStackLayout { Spacing = 10 };
            body.Add(Serif(quest.Name, 18));
            body.Add(Txt(quest.Description, 13, Theme.Stone600));

            for (var i = 0; i < quest.Objectives.Count; i++)
            {
                var o = quest.Objectives[i];
                var text = s.ObjectiveText(o);
                if (i < progress.Step)
                {
                    body.Add(IconRow(Icon(Ico.CircleCheck, 16, Theme.Green600), Txt(text, 13, Theme.Stone500)));
                }
                else if (i == progress.Step && !completed)
                {
                    var objective = Stack(IconRow(Icon(Ico.Target, 16, Theme.Gold600), Txt(text, 14, Theme.Stone900, bold: true)));
                    if (o.Type == ObjectiveType.Defeat && o.Count > 1)
                        objective.Add(Bar("", progress.Count, o.Count, Theme.Gold600, 6));
                    var current = Card(objective, Theme.ParchmentLight, Theme.Gold600.WithAlpha(0.4f), 10);
                    current.Padding = new Thickness(12, 10);
                    body.Add(current);
                }
                // Les objectifs suivants restent cachés : on découvre la suite en avançant.
            }

            var status = completed
                ? Badge("Accomplie", Theme.Green600)
                : Badge($"Étape {Math.Min(progress.Step + 1, quest.Objectives.Count)}/{quest.Objectives.Count}", Theme.Gold700);
            stack.Add(TitledCard(completed ? Ico.Trophy : Ico.Scroll, completed ? "Quête accomplie" : "Quête", body, status));
        }
        Content = stack;
    }
}
