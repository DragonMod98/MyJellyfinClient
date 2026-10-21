using Android.Views;
using MyJellyfinClient.Services;

namespace MyJellyfinClient.Platforms.Android.Services;

public class AndroidBrightnessService : IBrightnessService
{
    public void SetBrightness(float brightness)
    {
        var activity = Platform.CurrentActivity;
        if (activity?.Window == null) return;

        var attributes = activity.Window.Attributes;
        // Float-Wert an das Android Window Attribut übergeben
        attributes.ScreenBrightness = Math.Max(0.01f, Math.Min(1.0f, brightness));
        activity.Window.Attributes = attributes;
    }
}