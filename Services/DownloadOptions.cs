using System.Text.Json;

namespace MyJellyfinClient.Services;

public class ChoiceOption
{
    public ChoiceOption(string code, string name) { Code = code; Name = name; }
    public string Code { get; }
    public string Name { get; }
    public override string ToString() => Name;
}

public class TrackOption
{
    public int Index { get; set; }       // Stream-Index im Container; -1 = "Alle", -2 = "Keine"
    public string Language { get; set; } // ISO 639-2, z.B. "ger"
    public string Name { get; set; }
    public override string ToString() => Name;
}

/// <summary>Auswahl, die fuer einen Download gilt (Qualitaet, Tonspur, Untertitel).</summary>
public class DownloadOptions
{
    public string Quality { get; set; } = "Original"; // Original, 1080p, 720p
    public string MediaSourceId { get; set; }
    public int? AudioStreamIndex { get; set; }        // null = alle Tonspuren / Server-Standard
    public int? SubtitleStreamIndex { get; set; }     // null = alle, -1 = keine

    // Original mit allen Spuren => direkter Datei-Download, sonst Remux/Transcode ueber den Server
    public bool NeedsServerProcessing => Quality != "Original" || AudioStreamIndex != null || SubtitleStreamIndex != null;
}

public class MediaStreamInfo
{
    public string MediaSourceId { get; set; }
    public int Height { get; set; }
    public List<TrackOption> Audio { get; } = new();
    public List<TrackOption> Subtitles { get; } = new();
}

public static class DownloadDefaults
{
    public static readonly ChoiceOption NoDefault = new("", "Keine Vorgabe");

    public static readonly IReadOnlyList<ChoiceOption> Qualities = new[]
    {
        NoDefault, new ChoiceOption("Original", "Original"), new ChoiceOption("1080p", "1080p"), new ChoiceOption("720p", "720p")
    };

    private static readonly ChoiceOption[] Languages =
    {
        new("ger", "Deutsch"), new("eng", "Englisch"), new("jpn", "Japanisch"),
        new("fre", "Französisch"), new("spa", "Spanisch"), new("ita", "Italienisch")
    };

    public static readonly IReadOnlyList<ChoiceOption> AudioLanguages = new[] { NoDefault }.Concat(Languages).ToList();

    public static readonly IReadOnlyList<ChoiceOption> SubtitleLanguages =
        new[] { NoDefault, new ChoiceOption("none", "Keine Untertitel") }.Concat(Languages).ToList();

    // Jellyfin liefert mal "ger", mal "deu" - beides gleich behandeln
    public static string NormalizeLanguage(string code)
    {
        code = (code ?? string.Empty).Trim().ToLowerInvariant();
        return code switch
        {
            "deu" => "ger",
            "fra" => "fre",
            "ces" => "cze",
            "nld" => "dut",
            "zho" => "chi",
            _ => code
        };
    }
}

public static class MediaStreamParser
{
    public static MediaStreamInfo Parse(string playbackInfoJson)
    {
        var info = new MediaStreamInfo();
        using var doc = JsonDocument.Parse(playbackInfoJson);

        if (!doc.RootElement.TryGetProperty("MediaSources", out var sources) || sources.GetArrayLength() == 0)
            return info;

        var source = sources[0];
        if (source.TryGetProperty("Id", out var id))
            info.MediaSourceId = id.GetString();

        if (!source.TryGetProperty("MediaStreams", out var streams))
            return info;

        foreach (var s in streams.EnumerateArray())
        {
            string type = s.TryGetProperty("Type", out var t) ? t.GetString() : null;
            int index = s.TryGetProperty("Index", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt32() : -1;
            string lang = s.TryGetProperty("Language", out var l) ? l.GetString() : null;
            string title = s.TryGetProperty("DisplayTitle", out var d) ? d.GetString() : null;

            switch (type)
            {
                case "Video" when info.Height == 0 && s.TryGetProperty("Height", out var h) && h.ValueKind == JsonValueKind.Number:
                    info.Height = h.GetInt32();
                    break;
                case "Audio":
                    info.Audio.Add(new TrackOption { Index = index, Language = DownloadDefaults.NormalizeLanguage(lang), Name = title ?? lang ?? $"Tonspur {index}" });
                    break;
                case "Subtitle":
                    info.Subtitles.Add(new TrackOption { Index = index, Language = DownloadDefaults.NormalizeLanguage(lang), Name = title ?? lang ?? $"Untertitel {index}" });
                    break;
            }
        }

        return info;
    }

    /// <summary>
    /// Wendet Vorgaben (Qualitaet, Tonspur-Sprache, Untertitel-Sprache) auf ein Medium an.
    /// Leere Vorgabe = keine Einschraenkung. complete=false, wenn eine gesetzte Vorgabe nicht verfuegbar ist.
    /// </summary>
    public static DownloadOptions Resolve(MediaStreamInfo info, string quality, string audioLang, string subtitleLang, out bool complete)
    {
        complete = true;
        var options = new DownloadOptions { MediaSourceId = info?.MediaSourceId };
        if (info == null) { complete = string.IsNullOrEmpty(quality) && string.IsNullOrEmpty(audioLang) && string.IsNullOrEmpty(subtitleLang); return options; }

        // Qualitaet
        if (quality == "1080p")
        {
            if (info.Height == 0 || info.Height >= 1080) options.Quality = "1080p"; else complete = false;
        }
        else if (quality == "720p")
        {
            if (info.Height == 0 || info.Height > 720) options.Quality = "720p"; // sonst ist Original bereits <= 720p
        }

        // Tonspur
        if (!string.IsNullOrEmpty(audioLang))
        {
            var track = info.Audio.FirstOrDefault(a => a.Language == DownloadDefaults.NormalizeLanguage(audioLang));
            if (track != null) options.AudioStreamIndex = track.Index; else complete = false;
        }

        // Untertitel
        if (subtitleLang == "none")
        {
            options.SubtitleStreamIndex = -1;
        }
        else if (!string.IsNullOrEmpty(subtitleLang))
        {
            var track = info.Subtitles.FirstOrDefault(s => s.Language == DownloadDefaults.NormalizeLanguage(subtitleLang));
            if (track != null) options.SubtitleStreamIndex = track.Index; else complete = false;
        }

        return options;
    }
}
