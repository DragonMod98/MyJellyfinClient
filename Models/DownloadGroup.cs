using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MyJellyfinClient.Models;

/// <summary>
/// Gruppe in der Download-Uebersicht: eine Serie oder "Filme".
/// Die sichtbaren Eintraege (Collection) sind leer, solange die Gruppe eingeklappt ist.
/// </summary>
public class DownloadGroup : ObservableCollection<LocalMediaItem>
{
    private readonly List<LocalMediaItem> _all;
    private bool _isExpanded;

    public DownloadGroup(string name, bool isSeries, IEnumerable<LocalMediaItem> items, bool expanded)
    {
        Name = name;
        IsSeries = isSeries;
        _all = items.ToList();

        _isExpanded = expanded;
        if (expanded)
        {
            foreach (var item in _all) Add(item);
        }
    }

    public string Name { get; }
    public bool IsSeries { get; }

    public bool IsExpanded => _isExpanded;
    public string ExpandIcon => _isExpanded ? "▼" : "▶";

    public int TotalCount => _all.Count;
    public int WatchedCount => _all.Count(i => i.IsWatched);

    public string Subtitle
    {
        get
        {
            string noun = IsSeries
                ? (TotalCount == 1 ? "Folge" : "Folgen")
                : (TotalCount == 1 ? "Film" : "Filme");
            return $"{TotalCount} {noun} · {WatchedCount} gesehen";
        }
    }

    // Durchschnittlicher Fortschritt aller Eintraege (0..1)
    public double Progress => _all.Count == 0 ? 0 : _all.Average(i => i.Progress);

    public void Toggle()
    {
        _isExpanded = !_isExpanded;

        if (_isExpanded)
        {
            foreach (var item in _all) Add(item);
        }
        else
        {
            Clear();
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsExpanded)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(ExpandIcon)));
    }
}
