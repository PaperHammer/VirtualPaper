using System.Collections.ObjectModel;

namespace VirtualPaper.Models.Mvvm {
    [Serializable]
    public class ObservableList<T> : ObservableCollection<T> {
        public ObservableList() { }

        public ObservableList(IEnumerable<T> items) => AddRange(items);

        public void AddRange(IEnumerable<T> items) {
            if (items == null) return;

            foreach (var item in items) Add(item);
        }

        public void AddRangeReverse(IEnumerable<T> items) {
            if (items == null) return;

            AddRange(items.Reverse());
        }

        public void SetRange(IEnumerable<T> items) {
            if (items == null) return;

            ClearItems();
            AddRange(items);
        }

        public void SetRange(IEnumerable<T> items, Action<T>? configureItem = null) {
            if (items == null) return;

            ClearItems();

            foreach (var item in items) {
                Add(item);
                configureItem?.Invoke(item);
            }
        }

        public void SetRangeReverse(IEnumerable<T> items) {
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
