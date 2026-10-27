using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Util;
using MyJellyfinClient.Services;

namespace MyJellyfinClient
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, SupportsPictureInPicture = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // Home-Taste/Geste bei laufender Wiedergabe: im kleinen Fenster (Bild-im-Bild) weiterspielen
        protected override void OnUserLeaveHint()
        {
            base.OnUserLeaveHint();

            if (PipState.IsPlayerActive && Build.VERSION.SdkInt >= BuildVersionCodes.O && !IsInPictureInPictureMode)
            {
                try
                {
                    var builder = new PictureInPictureParams.Builder().SetAspectRatio(new Rational(16, 9));
                    EnterPictureInPictureMode(builder.Build());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PiP fehlgeschlagen: {ex.Message}");
                }
            }
        }

        public override void OnPictureInPictureModeChanged(bool isInPictureInPictureMode, Configuration newConfig)
        {
            base.OnPictureInPictureModeChanged(isInPictureInPictureMode, newConfig);
            PipState.RaiseModeChanged(isInPictureInPictureMode);
        }

        protected override void OnStop()
        {
            base.OnStop();

            // Fenster wurde im PiP-Modus weggewischt/geschlossen: Wiedergabe beenden
            if (PipState.IsPlayerActive && Build.VERSION.SdkInt >= BuildVersionCodes.N && IsInPictureInPictureMode)
                PipState.RaiseClosed();
        }
    }
}
