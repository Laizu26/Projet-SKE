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

        var stack = Stack(ButtonRow(
            Btn($"En cours ({active.Count})", () => { page.QuestsShowDone = false; page.Render(); }, selected: !page.QuestsShowDone),
            Btn($"Terminées ({done.Count})", () => { page.QuestsShowDone = true; page.Render(); }, selected: page.QuestsShowDone)));

        var list = page.QuestsShowDone ? done : active;
        if (list.Count == 0) stack.Add(Muted(page.QuestsShowDone ? "Aucune quête terminée." : "Aucune quête en cours. Parle aux habitants !"));

        foreach (var (quest, progress) in list)
        {
            var panel = Stack(Txt(quest.Name, 16, Theme.Accent, bold: true), Txt(quest.Description, 13));
            for (var i = 0; i < quest.Objectives.Count; i++)
            {
                var o = quest.Objectives[i];
                var text = s.ObjectiveText(o);
                if (i < progress.Step) panel.Add(Muted("✓ " + text));
                else if (i == progress.Step && progress.Status == QuestStatus.Active)
                {
                    var counter = o.Type == ObjectiveType.Defeat && o.Count > 1 ? $" ({progress.Count}/{o.Count})" : "";
                    panel.Add(Txt("▶ " + text + counter, 14, Theme.Text, bold: true));
                }
                // Les objectifs suivants restent cachés : on découvre la suite en avançant.
            }
            stack.Add(Panel(panel));
        }
        Content = stack;
    }
}
