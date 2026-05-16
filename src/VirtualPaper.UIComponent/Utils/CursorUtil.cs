using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace VirtualPaper.UIComponent.Utils {
    public class CursorUtil {
        private static readonly PropertyInfo ProtectedCursorProperty =
        typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;

        public static readonly DependencyProperty CursorShapeProperty =
            DependencyProperty.RegisterAttached(
                "CursorShape",
                typeof(InputSystemCursorShape),
                typeof(CursorUtil),
                new PropertyMetadata(InputSystemCursorShape.Arrow, OnCursorShapeChanged));

        public static InputSystemCursorShape GetCursorShape(DependencyObject obj) =>
            (InputSystemCursorShape)obj.GetValue(CursorShapeProperty);

        public static void SetCursorShape(DependencyObject obj, InputSystemCursorShape value) =>
            obj.SetValue(CursorShapeProperty, value);

        private static void OnCursorShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is UIElement element && e.NewValue is InputSystemCursorShape shape) {
                var cursor = InputSystemCursor.Create(shape);
                ProtectedCursorProperty.SetValue(element, cursor);
            }
        }
    }
}
