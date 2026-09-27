using System.Text.Json;
using System.Text.Json.Serialization;
using ProjetSKE.Core.State;

namespace ProjetSKE.Core.Systems;

/// <summary>Sauvegarde locale en JSON, 3 emplacements.</summary>
public sealed class SaveService
{
    public const int SlotCount = 3;

    private readonly string _directory;

    public SaveService(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    private string PathFor(int slot) => Path.Combine(_directory, $"save{slot}.json");

    public void Save(int slot, GameState state)
    {
        state.SavedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(state, SaveJsonContext.Default.GameState);
        var path = PathFor(slot);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    public GameState? Load(int slot)
    {
        var path = PathFor(slot);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), SaveJsonContext.Default.GameState);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool Exists(int slot) => File.Exists(PathFor(slot));

    public void Delete(int slot)
    {
        if (Exists(slot)) File.Delete(PathFor(slot));
    }
}

// Sérialisation générée à la compilation : fonctionne avec le "trimming" de l'APK Android.
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(GameState))]
internal partial class SaveJsonContext : JsonSerializerContext
{
}
