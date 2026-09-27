using Android.App;
using Android.Content.PM;
using Android.OS;

namespace ProjetSKE.App;

[Activity(
    Name = "com.laizu.projetske.MainActivity",
    Theme = "@style/Maui.MainTheme.NoActionBar",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Test automatique (émulateur de GitHub Actions) : adb shell am start ... --ez autotest true
        if (Intent?.GetBooleanExtra("autotest", false) == true) Dev.AutoTest.Requested = true;
        base.OnCreate(savedInstanceState);
    }
}
