using ProjetSKE.App.Ui;
using ProjetSKE.Core.Cloud;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>Réglages de la base en ligne (Firestore) du mode développeur.</summary>
public sealed class CloudSettingsPage : EditorPage
{
    private string _project;
    private string _key;
    private string _collection;
    private string _document;
    private string? _testResult;

    public CloudSettingsPage()
    {
        var s = CloudSync.Settings;
        (_project, _key, _collection, _document) = (s.ProjectId, s.ApiKey, s.Collection, s.Document);
        Render();
    }

    protected override string PageTitle => "Base de données";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    private CloudSettings Current => new(_project.Trim(), _key.Trim(), _collection.Trim(), _document.Trim());

    protected override void Build(Form f)
    {
        f.Note("Le contenu du mode développeur est rangé dans un document Firestore partagé : chaque téléphone configuré " +
               "récupère la dernière version au lancement et peut publier ses modifications.");
        f.BoolField("Synchronisation activée", CloudSync.Enabled, v => CloudSync.Enabled = v);
        f.TextField("Votre nom (affiché dans l'historique)", CloudSync.Author, v => CloudSync.Author = v);

        f.Header("Projet Firebase");
        f.TextField("ID du projet (ex : mon-jeu-12345)", _project, v => _project = v);
        f.TextField("Clé API Web", _key, v => _key = v);
        f.TextField("Collection", _collection, v => _collection = v);
        f.TextField("Document", _document, v => _document = v);
        f.Note("Ces informations restent sur ce téléphone et ne sont jamais envoyées dans le dépôt GitHub.");

        f.Add(ButtonRow(
            Btn("Enregistrer", () =>
            {
                CloudSync.Settings = Current;
                _testResult = "Réglages enregistrés.";
                Render();
            }, selected: true),
            Btn("Tester", async () =>
            {
                _testResult = "Test en cours…";
                Render();
                _testResult = await CloudSync.TestAsync(Current);
                Render();
            })));
        if (_testResult is not null) f.Add(Card(Txt(_testResult, 13, Theme.Stone800)));

        f.Header("État");
        f.Note(CloudSync.LastStatus);
        f.Note(CloudSync.BaseUpdateTime is null
            ? "Aucune version en ligne connue sur ce téléphone."
            : $"Basé sur la révision {CloudSync.BaseRevision} ({CloudSync.BaseUpdateTime}).");
    }
}
