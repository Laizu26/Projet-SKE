using ProjetSKE.App.Pages;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.App.Dev;

/// <summary>
/// Parcours automatique du jeu, lancé sur l'émulateur de GitHub Actions
/// (adb shell am start ... --ez autotest true) pour détecter les plantages sans téléphone.
/// </summary>
public static class AutoTest
{
    public static bool Requested { get; set; }

    private static void Log(string message) => CrashReporter.Log("SKE_AUTOTEST " + message);

    private static async Task Step(string name, Action action, int waitMs = 1200)
    {
        Log("étape : " + name);
        await MainThread.InvokeOnMainThreadAsync(action);
        await Task.Delay(waitMs);
        Log("ok : " + name);
    }

    public static async Task RunAsync()
    {
        try
        {
            Log("début");
            var db = SkeApp.Db;
            var starter = db.Starters.First();

            await Step("écran de sélection", () => SkeApp.GoTo(new CharacterSelectPage(0)));

            GamePage? page = null;
            await Step("nouvelle partie", () =>
            {
                var session = GameSession.NewGame(db, starter.Id);
                SkeApp.Saves.Save(0, session.State);
                page = new GamePage(session, 0, playIntro: true);
                SkeApp.GoTo(page);
            }, 2500);

            foreach (var tab in Enum.GetValues<GameTab>())
                await Step("onglet " + tab, () => page!.SwitchTab(tab));

            await Step("carte du royaume", () => { page!.SwitchTab(GameTab.Map); page.MapShowCountry = true; page.Render(); });
            await Step("carte du lieu", () => { page!.MapShowCountry = false; page.Render(); });
            await Step("dialogue", () => page!.ShowDialogue(db.Content.Dialogues[0].Id), 2500);
            await Step("combat", () => page!.StartBattle(new[] { db.Content.Monsters[0].Id }), 2500);

            await Step("chargement d'une sauvegarde", () =>
            {
                var state = SkeApp.Saves.Load(0) ?? throw new InvalidOperationException("sauvegarde introuvable");
                SkeApp.GoTo(new GamePage(new GameSession(db, state), 0, playIntro: false));
            }, 2500);

            Log("base en ligne : " + await CloudSync.TestAsync(CloudSync.Settings));
            Log("SKE_AUTOTEST_DONE");
        }
        catch (Exception e)
        {
            Log("SKE_AUTOTEST_FAIL " + e);
        }
    }
}
