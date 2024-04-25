using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VirtualPaper.Models.Mvvm
{
    public class ObservableList<T> : List<T>, INotifyPropertyChanged, INotifyCollectionChanged
    {
        public ObservableList() { }

        public ObservableList(IEnumerable<T> items) => this.AddRange(items);

        public new void Add(T item)
        {
            base.Add(item);

            OnCollectionChanged(new(NotifyCollectionChangedAction.Add, item, Count - 1));
        }

        public new void AddRange(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            foreach (var item in items)
            {
                base.Add(item);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public new void RemoveAt(int idx)
        {
            T removedItem = this[idx];
            base.RemoveAt(idx);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, idx));
        }

        public void SetRange(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            Clear();
            this.AddRange(items);
        }

        public void SetValue(T item, int idx)
        {
            T oldItem = this[idx];
            this[idx] = item;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, idx));
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
            OnPropertyChanged(nameof(Count));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
