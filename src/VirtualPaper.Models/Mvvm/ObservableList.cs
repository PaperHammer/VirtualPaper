using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VirtualPaper.Models.Mvvm {
    [Serializable]
    public class ObservableList<T> : List<T>, INotifyPropertyChanged, INotifyCollectionChanged {
        public ObservableList() { }

        public ObservableList(IList<T> items) => AddRange(items);

        public new void Add(T item) {
            base.Add(item);
            OnCollectionChanged(new(NotifyCollectionChangedAction.Add, item, Count - 1));
        }

        public void AddRange(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            foreach (var item in items) {
                Add(item);
            }
            //OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items));
        }

        public void AddRangeReverse(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            for (int i = items.Count - 1; i >= 0; i--) {
                Add(items[i]);
            }
            //OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items));
        }

        public new void Clear() {
            base.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public new void RemoveAt(int idx) {
            T removedItem = this[idx];
            base.RemoveAt(idx);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove, removedItem, idx));
        }

        public void SetRange(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            Clear();
            AddRange(items);
        }

        public void SetRangeReverse(IList<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            Clear();
            AddRangeReverse(items);
        }

        public new void Insert(int index, T item) {
            if (index < 0 || index > this.Count) {
                throw new ArgumentOutOfRangeException(nameof(index), "Index must be within the bounds of the list.");
            }

            base.Insert(index, item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, item, index));
        }

        public void SetValue(T newItem, int idx) {
            T oldItem = this[idx];
            this[idx] = newItem;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Replace, newItem, oldItem, idx));
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            CollectionChanged?.Invoke(this, e);
            OnPropertyChanged(nameof(Count));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
