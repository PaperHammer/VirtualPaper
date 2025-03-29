using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace VirtualPaper.UIComponent.Input {
    public partial class ArcColorPicker : ColorPicker {
        public ArcColorPicker() {
            //this.Loaded += ArcColorPicker_Loaded;
            //this.ColorChanged += ArcColorPicker_ColorChanged;
        }

        //private void ArcColorPicker_Loaded(object sender, RoutedEventArgs e) {
        //    FindAndTraceVisualChild("CyanTextBox");
        //    FindAndTraceVisualChild("MagentaTextBox");
        //    FindAndTraceVisualChild("YellowTextBox");
        //    FindAndTraceVisualChild("BlackTextBox");
        //    RegistEvent();
        //}

        //private void RegistEvent() {
        //    (_controls["CyanTextBox"] as TextBox).TextChanged += ArcColorPicker_CyanTextChanged;
        //    (_controls["MagentaTextBox"] as TextBox).TextChanged += ArcColorPicker_MagentaTextChanged;
        //    (_controls["YellowTextBox"] as TextBox).TextChanged += ArcColorPicker_YellowTextChanged;
        //    (_controls["BlackTextBox"] as TextBox).TextChanged += ArcColorPicker_BlackTextChanged;
        //}

        //private void ArcColorPicker_BlackTextChanged(object sender, TextChangedEventArgs e) {
        //    if ((sender as Control).FocusState.Equals(FocusState.Unfocused)) return;

        //    double c = (_controls["CyanTextBox"] as ArcTextNumberBox).GetValue();
        //    double m = (_controls["MagentaTextBox"] as ArcTextNumberBox).GetValue();
        //    double y = (_controls["YellowTextBox"] as ArcTextNumberBox).GetValue();
        //    double k = (_controls["BlackTextBox"] as ArcTextNumberBox).GetValue();
        //    this.Color = Windows.UI.Color.FromArgb(this.Color.A, (byte)Math.Round((255 * (1 - c / 100) * (1 - k / 100))), (byte)Math.Round((255 * (1 - m / 100) * (1 - k / 100))), (byte)Math.Round((255 * (1 - y / 100) * (1 - k / 100))));
        //}

        //private void ArcColorPicker_YellowTextChanged(object sender, TextChangedEventArgs e) {
        //    if (!(sender as ArcTextNumberBox).IsFocused()) return;

        //    double y = (_controls["YellowTextBox"] as ArcTextNumberBox).GetValue();
        //    double k = (_controls["BlackTextBox"] as ArcTextNumberBox).GetValue();
        //    this.Color = Windows.UI.Color.FromArgb(this.Color.A, this.Color.R, this.Color.G, (byte)(255 * (1 - y / 100) * (1 - k / 100)));
        //}

        //private void ArcColorPicker_MagentaTextChanged(object sender, TextChangedEventArgs e) {
        //    if (!(sender as ArcTextNumberBox).IsFocused()) return;

        //    double m = (_controls["MagentaTextBox"] as ArcTextNumberBox).GetValue();
        //    double k = (_controls["BlackTextBox"] as ArcTextNumberBox).GetValue();
        //    this.Color = Windows.UI.Color.FromArgb(this.Color.A, this.Color.R, (byte)(255 * (1 - m / 100) * (1 - k / 100)), this.Color.B);
        //}

        //private void ArcColorPicker_CyanTextChanged(object sender, TextChangedEventArgs e) {
        //    if (!(sender as ArcTextNumberBox).IsFocused()) return;

        //    double c = (_controls["CyanTextBox"] as ArcTextNumberBox).GetValue();
        //    double k = (_controls["BlackTextBox"] as ArcTextNumberBox).GetValue();
        //    this.Color = Windows.UI.Color.FromArgb(this.Color.A, (byte)(255 * (1 - c / 100) * (1 - k / 100)), this.Color.B, this.Color.G);
        //}

        //private void ArcColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args) {
        //    UpdateCMYK(args);
        //}

        //private void UpdateCMYK(ColorChangedEventArgs args) {           
        //    if ((_controls["CyanTextBox"] as ArcTextNumberBox).IsFocused() ||
        //        (_controls["MagentaTextBox"] as ArcTextNumberBox).IsFocused() ||
        //        (_controls["YellowTextBox"] as ArcTextNumberBox).IsFocused() ||
        //        (_controls["BlackTextBox"] as ArcTextNumberBox).IsFocused()) return;

        //    double r = args.NewColor.R / 255.0;
        //    double g = args.NewColor.G / 255.0;
        //    double b = args.NewColor.B / 255.0;

        //    double k = 1.0 - Math.Max(r, Math.Max(g, b));
        //    double c = (1.0 - r - k) / (1.0 - k);
        //    double m = (1.0 - g - k) / (1.0 - k);
        //    double y = (1.0 - b - k) / (1.0 - k);

        //    (_controls["CyanTextBox"] as ArcTextNumberBox).SetValue(c * 100);
        //    (_controls["MagentaTextBox"] as ArcTextNumberBox).SetValue(m * 100);
        //    (_controls["YellowTextBox"] as ArcTextNumberBox).SetValue(y * 100);
        //    (_controls["BlackTextBox"] as ArcTextNumberBox).SetValue(k * 100);
        //}

        //private void FindAndTraceVisualChild(string childName) {
        //    DependencyObject obj = FindVisualChildByName(this, childName);

        //    if (obj != null) {
        //        _controls[childName] = obj;
        //    }
        //}

        //public static DependencyObject FindVisualChildByName(FrameworkElement parent, string name) {
        //    if (parent.Name == name) {
        //        return parent;
        //    }

        //    int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        //    for (int i = 0; i < childrenCount; i++) {
        //        FrameworkElement childAsFE = VisualTreeHelper.GetChild(parent, i) as FrameworkElement;

        //        if (childAsFE != null) {
        //            DependencyObject result = FindVisualChildByName(childAsFE, name);

        //            if (result != null) {
        //                return result;
        //            }
        //        }
        //    }

        //    return null;
        //}

        //private readonly Dictionary<string, DependencyObject> _controls = [];
    }
}
