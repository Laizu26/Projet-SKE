using System.Net;
using System.Text;
using ProjetSKE.Core.Cloud;
using ProjetSKE.Core.Data;

namespace ProjetSKE.Core.Tests;

public class CloudTests
{
    /// <summary>Faux serveur : répond selon l'URL et garde les requêtes reçues.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.ToString(), body));
            var (status, text) = respond(request);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly CloudSettings Settings = new("mon-projet", "cle");

    private static (HttpStatusCode, string) Auth(HttpRequestMessage r) =>
        (HttpStatusCode.OK, "{\"idToken\":\"jeton\",\"expiresIn\":\"3600\"}");

    [Fact]
    public void Document_RoundTrip()
    {
        var content = GameDatabase.Default.Content;
        var json = FirestoreFormat.BuildDocument(content, 7, "Yann", new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc));
        var withTime = json.Insert(1, "\"updateTime\":\"2026-09-27T12:00:00.123456Z\",");
        var snap = FirestoreFormat.ParseDocument(withTime);
        Assert.Equal(7, snap.Revision);
        Assert.Equal("Yann", snap.UpdatedBy);
        Assert.Equal("2026-09-27T12:00:00.123456Z", snap.UpdateTime);
        Assert.Equal(content.Npcs.Count, snap.Content.Npcs.Count);
        Assert.Equal(ContentSerializer.ToJson(content), ContentSerializer.ToJson(snap.Content));
    }

    [Fact]
    public async Task Pull_ReturnsNull_WhenNothingPublished()
    {
        var handler = new FakeHandler(r => r.RequestUri!.Host.StartsWith("identitytoolkit") ? Auth(r) : (HttpStatusCode.NotFound, "{}"));
        var repo = new FirestoreContentRepository(Settings, new HttpClient(handler));
        Assert.Null(await repo.PullAsync());
        var get = handler.Requests.Last();
        Assert.Contains("projects/mon-projet/databases/(default)/documents/projet-ske/contenu", get.Url);
    }

    [Fact]
    public async Task Push_SendsPreconditionAndDetectsConflict()
    {
        var handler = new FakeHandler(r => r.RequestUri!.Host.StartsWith("identitytoolkit")
            ? Auth(r)
            : (HttpStatusCode.BadRequest, "{\"error\":{\"status\":\"FAILED_PRECONDITION\"}}"));
        var repo = new FirestoreContentRepository(Settings, new HttpClient(handler));
        var result = await repo.PushAsync(GameDatabase.Default.Content, "2026-01-01T00:00:00Z", 3, "Yann");
        Assert.Equal(PushStatus.Conflict, result.Status);
        var patch = handler.Requests.Last();
        Assert.Equal(HttpMethod.Patch, patch.Method);
        Assert.Contains("currentDocument.updateTime=", patch.Url);
        Assert.Contains("\"integerValue\":\"4\"", patch.Body);
    }

    [Fact]
    public async Task Push_Ok_ReturnsNewVersion()
    {
        var handler = new FakeHandler(r => r.RequestUri!.Host.StartsWith("identitytoolkit")
            ? Auth(r)
            : (HttpStatusCode.OK, "{\"updateTime\":\"2026-09-27T13:00:00Z\"}"));
        var repo = new FirestoreContentRepository(Settings, new HttpClient(handler));
        var result = await repo.PushAsync(GameDatabase.Default.Content, null, 0, "Yann", firstPublish: true);
        Assert.Equal(PushStatus.Ok, result.Status);
        Assert.Equal("2026-09-27T13:00:00Z", result.UpdateTime);
        Assert.Equal(1, result.Revision);
        var patches = handler.Requests.Where(r => r.Method == HttpMethod.Patch).ToList();
        Assert.Contains("currentDocument.exists=false", patches[0].Url);
        Assert.Contains("/historique/r000001", patches[1].Url); // copie de sauvegarde de la révision
    }
}

public class MergeTests
{
    private static Models.GameContent Copy() => ContentSerializer.Clone(GameDatabase.Default.Content);

    [Fact]
    public void DifferentElementsChanged_BothKept()
    {
        var @base = Copy();
        var local = Copy();
        var remote = Copy();
        local.Items.First(i => i.Id == "potion").Price = 25;
        remote.Monsters.First(m => m.Id == "loup").Xp = 99;
        local.Npcs.Add(new Models.NpcDef { Id = "nouveau_local", Name = "Local", LocationId = "havrefort" });
        remote.Quests.Add(new Models.QuestDef { Id = "nouvelle_en_ligne", Name = "En ligne" });

        var r = ContentMerger.Merge(@base, local, remote);
        Assert.Empty(r.Conflicts);
        Assert.True(r.HasLocalChanges);
        Assert.Equal(25, r.Merged.Items.First(i => i.Id == "potion").Price);
        Assert.Equal(99, r.Merged.Monsters.First(m => m.Id == "loup").Xp);
        Assert.Contains(r.Merged.Npcs, n => n.Id == "nouveau_local");
        Assert.Contains(r.Merged.Quests, q => q.Id == "nouvelle_en_ligne");
    }

    [Fact]
    public void SameElementChangedDifferently_RemoteKept_LocalSavedInConflict()
    {
        var @base = Copy();
        var local = Copy();
        var remote = Copy();
        local.Items.First(i => i.Id == "potion").Price = 25;
        remote.Items.First(i => i.Id == "potion").Price = 30;

        var r = ContentMerger.Merge(@base, local, remote);
        var conflict = Assert.Single(r.Conflicts);
        Assert.Equal("potion", conflict.Id);
        Assert.Equal(30, r.Merged.Items.First(i => i.Id == "potion").Price);

        Assert.True(ContentMerger.Restore(r.Merged, conflict));
        Assert.Equal(25, r.Merged.Items.First(i => i.Id == "potion").Price);
    }

    [Fact]
    public void DeletedOnOneSide_ModifiedOnOther_IsKept()
    {
        var @base = Copy();
        var local = Copy();
        var remote = Copy();
        local.Monsters.RemoveAll(m => m.Id == "gobelin");
        remote.Monsters.First(m => m.Id == "gobelin").Gold = 50;
        remote.Skills.RemoveAll(s => s.Id == "priere");

        var r = ContentMerger.Merge(@base, local, remote);
        Assert.Equal(50, r.Merged.Monsters.First(m => m.Id == "gobelin").Gold); // pas de suppression d'un travail
        Assert.DoesNotContain(r.Merged.Skills, s => s.Id == "priere");         // suppression propre, personne ne l'a modifié
    }

    [Fact]
    public void NoLocalChange_TakesRemote()
    {
        var @base = Copy();
        var remote = Copy();
        remote.Start.Gold = 500;
        var r = ContentMerger.Merge(@base, Copy(), remote);
        Assert.False(r.HasLocalChanges);
        Assert.Empty(r.Conflicts);
        Assert.Equal(500, r.Merged.Start.Gold);
    }

    [Fact]
    public void Conflicts_Serialize()
    {
        var list = new List<MergeConflict> { new() { Kind = "Objet", Id = "potion", Name = "Potion", LocalJson = "{}" } };
        var back = ContentMerger.ConflictsFromJson(ContentMerger.ConflictsToJson(list));
        Assert.Equal("potion", Assert.Single(back).Id);
    }
}

public class FirestoreErrorTests
{
    private sealed class OneAnswer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(request.RequestUri!.Host.StartsWith("identitytoolkit") ? HttpStatusCode.BadRequest : status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    [Fact]
    public async Task DocumentNotFound_MeansNothingPublished()
    {
        // Réponse réelle de Firestore quand le document n'existe pas encore (le chemin contient « databases »).
        const string body = "{\"error\":{\"code\":404,\"message\":\"Document \\\"projects/projet-ske-597e2/databases/(default)/documents/projet-ske/contenu\\\" not found.\",\"status\":\"NOT_FOUND\"}}";
        var repo = new FirestoreContentRepository(new CloudSettings("p", "k"), new HttpClient(new OneAnswer(HttpStatusCode.NotFound, body)));
        Assert.Null(await repo.PullAsync());
    }

    [Fact]
    public async Task MissingDatabase_IsAnError()
    {
        const string body = "{\"error\":{\"code\":404,\"message\":\"The database (default) does not exist for project p\",\"status\":\"NOT_FOUND\"}}";
        var repo = new FirestoreContentRepository(new CloudSettings("p", "k"), new HttpClient(new OneAnswer(HttpStatusCode.NotFound, body)));
        var e = await Assert.ThrowsAsync<HttpRequestException>(() => repo.PullAsync());
        Assert.Contains("n'existe pas", e.Message);
    }
}
