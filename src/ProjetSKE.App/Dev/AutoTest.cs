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

    private static readonly List<string> Failures = [];

    /// <summary>Une étape en échec est notée (avec la trace complète, ligne par ligne) et le parcours continue.</summary>
    private static async Task Step(string name, Action action, int waitMs = 1200)
    {
        Log("étape : " + name);
        try
        {
            await MainThread.InvokeOnMainThreadAsync(action);
            await Task.Delay(waitMs);
            Log("ok : " + name);
        }
        catch (Exception e)
        {
            Failures.Add(name);
            Log("ÉCHEC : " + name);
            foreach (var line in e.ToString().Split('\n')) Log("  | " + line.TrimEnd());
            await Task.Delay(waitMs);
        }
    }

    private static void Sync(string message) => CrashReporter.Log("SKE_SYNC_TEST " + message);

    /// <summary>
    /// Synchronisation de bout en bout sur un document de test séparé (le vrai contenu n'est pas touché) :
    /// premier envoi, modification enregistrée puis publiée, relecture en ligne, puis un « autre téléphone »
    /// (état oublié, contenu d'origine) qui doit récupérer la modification.
    /// </summary>
    private static async Task SyncScenario()
    {
        var original = CloudSync.Settings;
        var test = original with { Document = "autotest-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") };
        try
        {
            CloudSync.Settings = test;
            CloudSync.ForgetSyncState();
            await MainThread.InvokeOnMainThreadAsync(() => { SkeApp.ResetContent(); DevState.Revert(); });
            Sync("1 premier envoi : " + await CloudSync.SyncAsync());

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                DevState.Draft.Npcs[0].Name = "AUTOTEST-EDIT";
                DevState.Touch();
                DevState.Save();
            });
            Sync("2 après enregistrement : " + await CloudSync.SyncAsync() + $" · en attente : {CloudSync.PendingChanges}");

            var online = await new Core.Cloud.FirestoreContentRepository(test).PullAsync();
            var ok = online?.Content.Npcs[0].Name == "AUTOTEST-EDIT";
            CrashReporter.Log(ok ? $"SKE_SYNC_OK publié (révision {online!.Revision})" : $"SKE_SYNC_FAIL la modification n'est pas en ligne (lu : {online?.Content.Npcs[0].Name ?? "rien"})");

            // Autre téléphone : état oublié, contenu d'origine.
            CloudSync.ForgetSyncState();
            await MainThread.InvokeOnMainThreadAsync(() => { SkeApp.ResetContent(); DevState.Revert(); });
            Sync("3 autre téléphone : " + await CloudSync.SyncAsync());
            var received = SkeApp.Db.Content.Npcs[0].Name == "AUTOTEST-EDIT";
            CrashReporter.Log(received ? "SKE_SYNC_OK récupéré sur un autre téléphone" : $"SKE_SYNC_FAIL non récupéré (local : {SkeApp.Db.Content.Npcs[0].Name})");

            // Mise à jour de l'application avec un contenu local déjà modifié : rien ne doit repartir en arrière.
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                DevState.Draft.Npcs[1].Name = "AUTOTEST-EDIT-2";
                DevState.Touch();
                DevState.Save();
            });
            Sync("4 deuxième modification : " + await CloudSync.SyncAsync());
            var again = await new Core.Cloud.FirestoreContentRepository(test).PullAsync();
            var both = again?.Content.Npcs[0].Name == "AUTOTEST-EDIT" && again.Content.Npcs[1].Name == "AUTOTEST-EDIT-2";
            CrashReporter.Log(both ? $"SKE_SYNC_OK deux modifications en ligne (révision {again!.Revision})" : "SKE_SYNC_FAIL deuxième modification absente");
            foreach (var line in CloudSync.Journal.Take(8)) Sync("journal : " + line);
        }
        catch (Exception e)
        {
            CrashReporter.Log("SKE_SYNC_FAIL exception " + e.Message);
            foreach (var line in e.ToString().Split('\n')) Sync("  | " + line.TrimEnd());
        }
        finally
        {
            CloudSync.Settings = original;
            CloudSync.ForgetSyncState();
            await MainThread.InvokeOnMainThreadAsync(() => { SkeApp.ResetContent(); DevState.Revert(); });
        }
    }

    public static async Task RunAsync()
    {
        try
        {
            Log("début");
            Log("base en ligne : " + await CloudSync.TestAsync(CloudSync.Settings));
            await SyncScenario();
            await Updates.CheckAsync();
            Log($"mises à jour : version installée {Updates.CurrentVersion} · {Updates.Status}");
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

            // Nouveautés : portrait (banque d'images), camp, choix de qui parle, temps.
            var content = Core.Data.ContentSerializer.Clone(db.Content);
            content.Portraits.Add(new Core.Models.PortraitDef
            {
                Id = "test", Name = "Test", Aspect = 1, FocusX = 0.5, FocusY = 0.4, Zoom = 1.5,
                Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/PNG_transparency_demonstration_1.png/280px-PNG_transparency_demonstration_1.png",
            });
            foreach (var npc in content.Npcs) npc.PortraitId = "test";
            var testDb = new Core.Data.GameDatabase(content);
            GamePage? world = null;
            await Step("partie avec portraits", () =>
            {
                var session = GameSession.NewGame(testDb, starter.Id);
                session.Recruit(testDb.Content.Characters.First(c => c.Id != starter.Id).Id);
                if (testDb.Content.Camp.Tasks.Count > 0 && session.CampMembers.Count > 0)
                    session.SetCampTask(session.CampMembers[0].Id, testDb.Content.Camp.Tasks[0].Id);
                session.AdvanceTime(24 * 60);
                world = new GamePage(session, -1, playIntro: false);
                SkeApp.GoTo(world);
            }, 2500);
            await Step("camp : les gens", () => { world!.CampShowPeople = true; world.SwitchTab(GameTab.Camp); });
            await Step("camp : fiche d'un membre", () =>
            {
                world!.SelectedCampMember = world.Session.CampMembers.FirstOrDefault()?.Id;
                world.Render();
            });
            await Step("parler : choix de qui parle", () =>
            {
                var npc = world!.Session.VisibleNpcs.FirstOrDefault() ?? testDb.Content.Npcs[0];
                world.TalkTo(npc.Id);
            });
            await Step("dialogue avec portrait", () => world!.ShowDialogue(testDb.Content.Npcs.First(n => n.DefaultDialogueId is not null).DefaultDialogueId!), 2500);
            await Step("combat avec répliques", () => world!.StartBattle(new[] { testDb.Content.Monsters.Last().Id }), 2500);

            await Step("éditeur : accueil", () => SkeApp.GoTo(new DevHomePage()));
            await Step("éditeur : monde", () => SkeApp.GoTo(new WorldEditor()));
            await Step("éditeur : temps", () => SkeApp.GoTo(new TimeEditor()));
            await Step("éditeur : karma", () => SkeApp.GoTo(new ScaleEditor(karma: true)));
            await Step("éditeur : campement", () => SkeApp.GoTo(new CampEditor()));
            await Step("éditeur : tâche", () => SkeApp.GoTo(new CampTaskEditor(DevState.Draft.Camp.Tasks[0])));
            await Step("éditeur : PNJ", () => SkeApp.GoTo(new NpcEditor(DevState.Draft.Npcs[0])));
            await Step("éditeur : monstre", () => SkeApp.GoTo(new MonsterEditor(DevState.Draft.Monsters.Last())));
            await Step("éditeur : dialogue", () => SkeApp.GoTo(new DialogueEditor(DevState.Draft.Dialogues.First(d => d.Nodes.Any(n => n.Variants.Count > 0)))), 2000);
            await Step("éditeur : départs", () => SkeApp.GoTo(new StartsPage()));
            await Step("éditeur : départ secondaire", () =>
            {
                if (DevState.Draft.ExtraStarts.Count > 0) SkeApp.GoTo(new StartEditor(DevState.Draft.ExtraStarts[0], main: false));
            });
            await Step("éditeur : quête à étapes", () =>
            {
                if (DevState.Draft.Quests.FirstOrDefault(q => q.IsStaged) is { } quest) SkeApp.GoTo(new QuestEditor(quest));
            });
            await Step("éditeur : étape de quête", () =>
            {
                if (DevState.Draft.Quests.FirstOrDefault(q => q.IsStaged) is { } quest) SkeApp.GoTo(new QuestStageEditor(quest, quest.Stages[1]));
            });
            await Step("choix du départ", () => SkeApp.GoTo(new StartSelectPage(0, db, starter.Id)));
            await Step("partie avec un autre départ", () =>
            {
                var other = db.StartsFor(starter.Id).LastOrDefault()?.Id;
                var session = GameSession.NewGame(db, starter.Id, other);
                var game = new GamePage(session, -1, playIntro: true);
                SkeApp.GoTo(game);
                game.CampShowPeople = false;
                game.SwitchTab(GameTab.Quests);
            }, 2500);
            await Step("éditeur : image", () =>
            {
                var image = new Core.Models.PortraitDef { Id = "img", Name = "Image", Url = content.Portraits[0].Url, Aspect = 1 };
                SkeApp.GoTo(new ImageEditor(image));
            }, 2000);
            DevState.Revert();

            await Step("chargement d'une sauvegarde", () =>
            {
                var state = SkeApp.Saves.Load(0) ?? throw new InvalidOperationException("sauvegarde introuvable");
                SkeApp.GoTo(new GamePage(new GameSession(db, state), 0, playIntro: false));
            }, 2500);

            Log(Failures.Count == 0 ? "SKE_AUTOTEST_DONE" : "SKE_AUTOTEST_FAIL étapes en échec : " + string.Join(", ", Failures));
        }
        catch (Exception e)
        {
            foreach (var line in e.ToString().Split('\n')) Log("  | " + line.TrimEnd());
            Log("SKE_AUTOTEST_FAIL " + e.Message);
        }
    }
}
