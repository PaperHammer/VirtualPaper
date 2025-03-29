using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VirtualPaper.Models.Mvvm {
    [Serializable]
    public class ObservableList<T> : List<T>, INotifyPropertyChanged, INotifyCollectionChanged {
        public ObservableList() { }

        public ObservableList(IEnumerable<T> items) => AddRange(items);

        public new void Add(T item) {
            base.Add(item);
            OnCollectionChanged(new(NotifyCollectionChangedAction.Add, item, Count - 1));
        }

        public new void AddRange(IEnumerable<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            foreach (var item in items) {
                Add(item);
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
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, idx));
        }

        public void SetRange(IEnumerable<T> items) {
            ArgumentNullException.ThrowIfNull(items);

            Clear();
            AddRange(items);
        }

        public void SetValue(T newItem, int idx) {
            T oldItem = this[idx];
            this[idx] = newItem;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem, idx));
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
