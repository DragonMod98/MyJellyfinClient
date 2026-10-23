using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyJellyfinClient.Services;

/// <summary>Ein laufender oder wartender Download (fuer die Anzeige in der Download-Uebersicht).</summary>
public partial class ActiveDownload : ObservableObject
{
    public string ItemId { get; init; }
    public string Title { get; init; }

    [ObservableProperty]
    private double progress;          // 0..1

    [ObservableProperty]
    private bool isIndeterminate;     // Groesse unbekannt

    [ObservableProperty]
    private string statusText = "Wartet...";
}

/// <summary>
/// Gemeinsamer Zustand aller laufenden Downloads. Wird vom Download-Service aktualisiert
/// und von der Download-Seite angezeigt. Alle Aenderungen laufen auf dem UI-Thread.
/// </summary>
public static class DownloadTracker
{
    public static ObservableCollection<ActiveDownload> Items { get; } = new();

    // Wird ausgeloest, wenn sich der Bestand an fertigen Downloads geaendert haben kann
    public static event Action Changed;

    public static void Register(string itemId, string title)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Items.Any(i => i.ItemId == itemId)) return;
            Items.Add(new ActiveDownload { ItemId = itemId, Title = title });
        });
    }

    // progress: 0..1, negativ = unbestimmt
    public static void Report(string itemId, double progress)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item == null) return;

            item.IsIndeterminate = progress < 0;
            item.Progress = Math.Max(progress, 0);
            item.StatusText = progress < 0 ? "Wird vorbereitet..." : $"{(int)(progress * 100)} %";
        });
    }

    public static void Remove(string itemId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item != null) Items.Remove(item);
            Changed?.Invoke();
        });
    }
}
