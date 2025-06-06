using System.Collections.ObjectModel;

namespace VirtualPaper.Models.Mvvm {
    [Serializable]
    public class ObservableList<T> : ObservableCollection<T> {
        public ObservableList() { }

        public ObservableList(IList<T> items) => AddRange(items);

        public void AddRange(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            for (int i = 0; i < items.Count; i++) { 
                InsertItem(i, items[i]);
            }
        }

        public void AddRangeReverse(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            for (int i = items.Count - 1; i >= 0; i--) {
                InsertItem(i, items[i]);
            }
        }

        public void SetRange(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            ClearItems();
            AddRange(items);
        }

        public void SetRangeReverse(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            ClearItems();
            AddRangeReverse(items);
        }

        public int FindIndex(T item) { 
            return IndexOf(item);
        }

        public int FindIndex(Predicate<T> match) {
            ArgumentNullException.ThrowIfNull(match);

            for (int i = 0; i < Count; i++) {
                if (match(this[i]))
                    return i;
            }

            return -1;
        }
    }
}
