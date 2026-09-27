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
        Assert.Contains("currentDocument.exists=false", handler.Requests.Last().Url);
    }
}
