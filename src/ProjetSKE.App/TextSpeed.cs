namespace ProjetSKE.App;

/// <summary>Vitesse d'écriture des dialogues (réglage du téléphone, dans le Menu).</summary>
public static class TextSpeed
{
    public static readonly (string Name, int CharsPerSecond)[] Levels =
    [
        ("Lente", 18),
        ("Normale", 30),
        ("Rapide", 60),
        ("Instantanée", 0),
    ];

    private const string Key = "text.speed";

    /// <summary>Index du niveau choisi (par défaut : Normale, volontairement posée).</summary>
    public static int Level
    {
        get => Math.Clamp(Preferences.Default.Get(Key, 1), 0, Levels.Length - 1);
        set => Preferences.Default.Set(Key, value);
    }

    public static string Name => Levels[Level].Name;

    /// <summary>Caractères par seconde (0 = tout le texte d'un coup).</summary>
    public static int CharsPerSecond => Levels[Level].CharsPerSecond;

    public static void Next() => Level = (Level + 1) % Levels.Length;
}
