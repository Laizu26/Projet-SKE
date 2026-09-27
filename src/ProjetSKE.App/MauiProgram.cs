namespace ProjetSKE.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<SkeApp>();
        return builder.Build();
    }
}
