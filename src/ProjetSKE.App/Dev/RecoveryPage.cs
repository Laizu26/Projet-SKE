using ProjetSKE.App.Ui;
using ProjetSKE.Core.Cloud;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>
/// Historique et récupération : toutes les révisions publiées en ligne, les copies gardées sur ce téléphone,
/// et le journal de synchronisation. Remettre une version ne supprime rien : la version actuelle est copiée avant,
/// puis la version remise est publiée comme nouvelle révision.
/// </summary>
public sealed class RecoveryPage : ContentPage
{
    private IReadOnlyList<CloudSnapshot>? _history;
    private string? _historyError;
    private string? _message;
    private bool _loading;
    private string? _confirm;

    public RecoveryPage()
    {
        Background = Theme.PageBackground;
        Render();
        _ = LoadHistory();
    }

    private async Task LoadHistory()
    {
        _loading = true;
        Render();
        try
        {
            _history = await CloudSync.HistoryAsync();
            _historyError = null;
        }
        catch (Exception e)
        {
            _historyError = e.Message;
        }
        _loading = false;
        Render();
    }

    private void Render()
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(12), Spacing = 8 };
        stack.Add(Row(Heading("Récupération"), Form.SmallButton("◂ Menu", () => SkeApp.GoTo(new DevHomePage()))));
        stack.Add(Muted("Rien n'est jamais effacé : avant chaque remise, la version actuelle du téléphone est copiée, " +
                        "puis la version choisie est publiée comme nouvelle révision."));
        if (_message is not null) stack.Add(Txt(_message, 13, Theme.Good, bold: true));

        stack.Add(Txt("Ce téléphone : " + ContentMerger.Summary(SkeApp.Db.Content), 12, Theme.Stone700));
        stack.Add(Btn("Récupérer la dernière version en ligne", async () =>
        {
            _message = await CloudSync.TakeOnlineAsync();
            Render();
        }));

        // Révisions en ligne.
        stack.Add(Section("Révisions en ligne"));
        if (_loading) stack.Add(Muted("Chargement…"));
        else if (_historyError is not null) stack.Add(Txt(_historyError, 12, Theme.Danger));
        else if (_history is { Count: 0 }) stack.Add(Muted("Aucune révision publiée."));
        foreach (var rev in _history ?? [])
        {
            var r = rev;
            var key = "rev" + rev.Revision;
            var info = Stack(
                Txt($"Révision {rev.Revision} · {rev.UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm} · {rev.UpdatedBy}", 14, Theme.Text, bold: true),
                Muted(ContentMerger.Summary(rev.Content), 11),
                Muted($"{ContentMerger.CountDifferences(rev.Content, SkeApp.Db.Content)} élément(s) différent(s) de ce téléphone", 11));
            stack.Add(Panel(Stack(info, Form.SmallButton(_confirm == key ? "Confirmer la remise ?" : "Remettre cette version", async () =>
            {
                if (_confirm != key) { _confirm = key; Render(); return; }
                _confirm = null;
                _message = await CloudSync.RestoreAsync(r.Content, $"révision en ligne {r.Revision}");
                await LoadHistory();
            }))));
        }

        // Copies locales.
        stack.Add(Section("Copies sur ce téléphone"));
        var backups = CloudSync.Backups;
        if (backups.Count == 0) stack.Add(Muted("Aucune copie."));
        foreach (var (path, when, label) in backups)
        {
            var key = "bak" + path;
            var content = CloudSync.ReadBackup(path);
            var info = Stack(
                Txt($"{when:dd/MM/yyyy HH:mm:ss} · {label}", 14, Theme.Text, bold: true),
                Muted(content is null ? "Copie illisible" : ContentMerger.Summary(content), 11));
            if (content is not null)
                info.Add(Muted($"{ContentMerger.CountDifferences(content, SkeApp.Db.Content)} élément(s) différent(s) de ce téléphone", 11));
            stack.Add(Panel(Stack(info, Form.SmallButton(_confirm == key ? "Confirmer la remise ?" : "Remettre cette copie", async () =>
            {
                if (content is null) return;
                if (_confirm != key) { _confirm = key; Render(); return; }
                _confirm = null;
                _message = await CloudSync.RestoreAsync(content, $"copie du {when:dd/MM HH:mm}");
                Render();
            }))));
        }

        // Journal.
        stack.Add(Section("Journal de synchronisation"));
        var journal = CloudSync.Journal;
        if (journal.Count == 0) stack.Add(Muted("Vide."));
        var box = Stack();
        foreach (var line in journal.Take(40)) box.Add(Txt(line, 11, line.Contains('⚠') ? Theme.Danger : Theme.Stone700));
        if (journal.Count > 0) stack.Add(Panel(box));

        Content = new ScrollView { Content = stack };
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(new DevHomePage());
        return true;
    }
}
