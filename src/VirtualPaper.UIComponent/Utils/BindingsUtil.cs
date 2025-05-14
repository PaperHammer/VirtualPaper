using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace VirtualPaper.UIComponent.Utils {
    public static class BindingsUtil {
        public static void ApplyBindings(FrameworkElement target, params BindingInfo[] bindingInfo) {
            foreach (var binding in bindingInfo) {
                var bindingExpression = new Binding {
                    Path = new PropertyPath(binding.SourcePath),
                    Source = binding.DataSource,
                    Mode = BindingMode.OneWay,
                    Converter = binding.Converter,                   
                };

                target.SetBinding(binding.TargetProperty, bindingExpression);
            }
        }
    }

    public record BindingInfo() {
        public BindingInfo(DependencyProperty targetProperty, string sourcePath, BindingMode mode)
            : this() {
            TargetProperty = targetProperty;
            SourcePath = sourcePath;
            Mode = mode;
        }

        public BindingInfo(DependencyProperty targetProperty, string sourcePath, BindingMode mode, IValueConverter converter) 
            : this(targetProperty, sourcePath, mode) {
            Converter = converter;
        }

        public DependencyProperty TargetProperty { get; set; }
        public object DataSource { get; set; }
        public string SourcePath { get; set; }
        public BindingMode Mode { get; set; }
        public IValueConverter Converter { get; set; }
    }
}
