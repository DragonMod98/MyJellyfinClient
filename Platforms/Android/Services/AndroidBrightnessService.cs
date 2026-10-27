using Android.Content;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Views;
using AndroidX.Core.View;
using MyJellyfinClient.Services;

namespace MyJellyfinClient.Platforms.Android.Services;

public class AndroidBrightnessService : IBrightnessService
{
    public void SetBrightness(float brightness)
    {
        var activity = Platform.CurrentActivity;
        if (activity?.Window == null) return;

        var attributes = activity.Window.Attributes;
        attributes.ScreenBrightness = Math.Max(0.01f, Math.Min(1.0f, brightness));
        activity.Window.Attributes = attributes;
    }

    public float GetBrightness()
    {
        var activity = Platform.CurrentActivity;
        if (activity?.Window == null) return 0.5f;

        // Fenster hat noch keinen eigenen Wert (-1) => Systemhelligkeit (0..255) verwenden
        float current = activity.Window.Attributes.ScreenBrightness;
        if (current >= 0) return current;
        try
        {
            int sys = Settings.System.GetInt(activity.ContentResolver, Settings.System.ScreenBrightness);
            return Math.Clamp(sys / 255f, 0.01f, 1f);
        }
        catch { return 0.5f; }
    }

    public void ResetBrightness()
    {
        var activity = Platform.CurrentActivity;
        if (activity?.Window == null) return;

        var attributes = activity.Window.Attributes;
        attributes.ScreenBrightness = -1f; // BRIGHTNESS_OVERRIDE_NONE
        activity.Window.Attributes = attributes;
    }

    private static AudioManager Audio =>
        (AudioManager)Platform.AppContext.GetSystemService(Context.AudioService);

    public float GetVolume()
    {
        var am = Audio;
        int max = am.GetStreamMaxVolume(Stream.Music);
        return max <= 0 ? 0f : am.GetStreamVolume(Stream.Music) / (float)max;
    }

    public void SetVolume(float volume)
    {
        var am = Audio;
        int max = am.GetStreamMaxVolume(Stream.Music);
        int index = (int)Math.Round(Math.Clamp(volume, 0f, 1f) * max);
        am.SetStreamVolume(Stream.Music, index, VolumeNotificationFlags.RemoveSoundAndVibrate);
    }

    public void SetImmersive(bool enabled)
    {
        var window = Platform.CurrentActivity?.Window;
        if (window?.DecorView == null) return;

        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller == null) return;

        if (enabled)
        {
            // Leisten per Wischen vom Rand voruebergehend einblenden
            controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            controller.Hide(WindowInsetsCompat.Type.SystemBars());

            // Video auch hinter der Kamera-Aussparung anzeigen
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                var attrs = window.Attributes;
                attrs.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
                window.Attributes = attrs;
            }
        }
        else
        {
            controller.Show(WindowInsetsCompat.Type.SystemBars());
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                var attrs = window.Attributes;
                attrs.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Default;
                window.Attributes = attrs;
            }
        }
    }
}
