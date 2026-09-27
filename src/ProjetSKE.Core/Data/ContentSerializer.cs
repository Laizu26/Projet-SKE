using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjetSKE.Core.Cloud;
using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>Lecture / écriture du contenu du jeu en JSON (fichier exporté par l'éditeur).</summary>
public static class ContentSerializer
{
    /// <summary>Nom de la ressource embarquée : si le fichier Data/content.json existe, il devient le contenu officiel.</summary>
    private const string EmbeddedName = "ProjetSKE.Core.Data.content.json";

    public static string ToJson(GameContent content) =>
        JsonSerializer.Serialize(content, ContentJsonContext.Default.GameContent);

    public static GameContent FromJson(string json) =>
        JsonSerializer.Deserialize(json, ContentJsonContext.Default.GameContent)
        ?? throw new JsonException("Fichier de contenu vide.");

    /// <summary>Copie indépendante (pour éditer sans toucher au contenu actif).</summary>
    public static GameContent Clone(GameContent content) => FromJson(ToJson(content));

    /// <summary>Contenu officiel : content.json embarqué s'il existe, sinon le contenu d'exemple.</summary>
    public static GameContent LoadDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedName);
        if (stream is null) return SampleContent.Build();
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GameContent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<MergeConflict>))]
internal partial class ContentJsonContext : JsonSerializerContext
{
}
