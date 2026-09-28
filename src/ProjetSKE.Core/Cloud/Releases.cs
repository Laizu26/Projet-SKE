using System.Text.Json;

namespace ProjetSKE.Core.Cloud;

/// <summary>Version publiée sur GitHub (Releases), avec son APK.</summary>
public sealed record ReleaseInfo(int Build, string Name, string Notes, string ApkUrl, long Size, DateTime PublishedAt)
{
    /// <summary>
    /// Lit la réponse de l'API GitHub « releases/latest ». Le numéro de version vient de l'étiquette
    /// « v123 » (numéro de compilation, qui est aussi le versionCode Android). Null si pas d'APK.
    /// </summary>
    /// <param name="extension">Fichier cherché dans la Release : « .apk » (téléphone) ou « .zip » (PC).</param>
    public static ReleaseInfo? Parse(string json, string extension = ".apk")
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        if (!int.TryParse(tag.TrimStart('v', 'V'), out var build)) return null;
        if (!root.TryGetProperty("assets", out var assets)) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            var size = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
            var published = root.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
                && DateTime.TryParse(p.GetString(), out var d) ? d : DateTime.MinValue;
            return new ReleaseInfo(
                build,
                root.TryGetProperty("name", out var rn) ? rn.GetString() ?? tag : tag,
                root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                url, size, published);
        }
        return null;
    }
}
