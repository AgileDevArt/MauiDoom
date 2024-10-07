using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace MauiDoom
{
    [Activity(Theme = "@style/MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var uiOptions =
                SystemUiFlags.HideNavigation |
                SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen |
                SystemUiFlags.Fullscreen |
                SystemUiFlags.LayoutStable |
                SystemUiFlags.ImmersiveSticky;

            if (Window == null) return;
            Window.DecorView.SystemUiFlags = uiOptions;
            this.Window?.AddFlags(WindowManagerFlags.Fullscreen);
        }
    }
}
