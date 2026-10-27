namespace MyJellyfinClient.Services;

/// <summary>Plattform-Zugriff fuer den Player: Helligkeit, Systemlautstaerke und Vollbild.</summary>
public interface IBrightnessService
{
    // Akzeptiert Werte von 0.0f (dunkel) bis 1.0f (hell)
    void SetBrightness(float brightness);

    // Aktuelle Fenster-/Systemhelligkeit (0..1)
    float GetBrightness();

    // Zurueck zur System-Helligkeitseinstellung
    void ResetBrightness();

    // Systemlautstaerke (Medien) 0..1
    float GetVolume();
    void SetVolume(float volume);

    // Statusleiste/Navigationsleiste aus- bzw. einblenden
    void SetImmersive(bool enabled);
}
