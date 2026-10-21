namespace MyJellyfinClient.Services;

public interface IBrightnessService
{
    // Akzeptiert Werte von 0.0f (dunkel) bis 1.0f (hell)
    void SetBrightness(float brightness);
}