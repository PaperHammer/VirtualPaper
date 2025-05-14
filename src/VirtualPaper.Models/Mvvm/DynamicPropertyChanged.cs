using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace VirtualPaper.Models.Mvvm {
    public partial class DynamicPropertyChanged : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected T Get<T>(Expression<Func<T>> propertyExpression) {
            var propertyName = GetPropertyName(propertyExpression);

            return (T)(_properties.TryGetValue(propertyName, out object? value) ? value : default(T));
        }

        protected void Set<T>(Expression<Func<T>> propertyExpression, T setValue) {
            var propertyName = GetPropertyName(propertyExpression);
            if (!_properties.TryGetValue(propertyName, out object? value) || !value.Equals(setValue)) {
                _properties[propertyName] = setValue;
                OnPropertyChanged(propertyName);
            }
        }

        private static string GetPropertyName<T>(Expression<Func<T>> propertyExpression) {
            if (propertyExpression.Body is not MemberExpression memberExpression) {
                throw new ArgumentException("The expression is not a member access expression.");
            }

            var property = memberExpression.Member as PropertyInfo 
                ?? throw new ArgumentException("The member access expression does not access a property.");
            
            return property.Name;
        }

        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly Dictionary<string, object> _properties = [];
    }
}
