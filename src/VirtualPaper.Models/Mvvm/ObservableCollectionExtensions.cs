using System.Collections.ObjectModel;
using VirtualPaper.Common.Utils.ThreadContext;

namespace VirtualPaper.Models.Mvvm {
    public static class ObservableCollectionExtensions {
        public static void Insert<T>(this ObservableCollection<T> collection, T item, int insertIdx = 0) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                collection.Insert(insertIdx, item);
            });
        }

        public static void InsertRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items, int insertIdx = 0) {
            if (items == null) return;

            var list = items is IList<T> lt ? lt : [.. items];

            CrossThreadInvoker.InvokeOnUIThread(() => {
                for (int i = list.Count - 1; i >= 0; i--) {
                    collection.Insert(insertIdx, list[i]);
                }
            });
        }

        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items) {
            if (items == null) return;

            CrossThreadInvoker.InvokeOnUIThread(() => {
                foreach (var item in items)
                    collection.Add(item);
            });
        }

        public static void AddRangeReverse<T>(this ObservableCollection<T> collection, IEnumerable<T> items) {
            if (items == null) return;

            collection.AddRange(items.Reverse());
        }

        public static void SetRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items) {
            if (items == null) return;

            collection.Clear();
            collection.AddRange(items);
        }

        public static void SetRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items, Action<T>? configureItem) {
            if (items == null) return;

            collection.Clear();

            CrossThreadInvoker.InvokeOnUIThread(() => {
                foreach (var item in items) {
                    collection.Add(item);
                    configureItem?.Invoke(item);
                }
            });
        }

        public static void SetRangeReverse<T>(this ObservableCollection<T> collection, IEnumerable<T> items) {
            if (items == null) return;

            CrossThreadInvoker.InvokeOnUIThread(() => {
                collection.Clear();
                collection.AddRange(items.Reverse());
            }, true);
        }

        public static int FindIndex<T>(this ObservableCollection<T> collection, T item) {
            return collection.IndexOf(item);
        }

        public static int FindIndex<T>(this ObservableCollection<T> collection, Predicate<T> match) {
            if (match == null) return -1;

            for (int i = 0; i < collection.Count; i++) {
                if (match(collection[i]))
                    return i;
            }

            return -1;
        }
    }
}
