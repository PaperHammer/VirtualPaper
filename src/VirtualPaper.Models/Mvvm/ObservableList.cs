using System.Collections.ObjectModel;

namespace VirtualPaper.Models.Mvvm {
    [Serializable]
    public class ObservableList<T> : ObservableCollection<T> {
        public ObservableList() { }

        public ObservableList(IList<T> items) => AddRange(items);

        public void AddRange(IList<T> items) {
            if (items == null) return;

            for (int i = 0; i < items.Count; i++) { 
                InsertItem(i, items[i]);
            }
        }

        public void AddRangeReverse(IList<T> items) {
            if (items == null) return;

            int n = items.Count;
            for (int i = n - 1; i >= 0; i--) {
                InsertItem(n - i - 1, items[i]);
            }
        }

        public void SetRange(IList<T> items) {
            if (items == null) return;

            ClearItems();
            AddRange(items);
        }

        public void SetRangeReverse(IList<T> items) {
            if (items == null) return;

            ClearItems();
            AddRangeReverse(items);
        }

        public int FindIndex(T item) { 
            return IndexOf(item);
        }

        public int FindIndex(Predicate<T> match) {
            if (match == null) return -1;

            for (int i = 0; i < Count; i++) {
                if (match(this[i]))
                    return i;
            }

            return -1;
        }
    }
}
