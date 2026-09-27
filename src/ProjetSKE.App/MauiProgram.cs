namespace ProjetSKE.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<SkeApp>()
            .ConfigureFonts(fonts => fonts.AddFont("lucide.ttf", "Lucide"));
        return builder.Build();
    }
}
