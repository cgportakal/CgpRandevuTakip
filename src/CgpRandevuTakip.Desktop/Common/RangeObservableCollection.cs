using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CgpRandevuTakip.Desktop.Common;

/// <summary>
/// Birden çok öğeyi tek bir Reset bildirimiyle değiştiren ObservableCollection.
/// Liste yenilemelerinde öğe başına CollectionChanged/PropertyChanged üretilmez.
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>Mevcut içeriği verilen öğelerle tek seferde değiştirir.</summary>
    public void ReplaceWith(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
