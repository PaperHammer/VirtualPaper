using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace VirtualPaper.UIComponent.Input {
    partial class ArcTextNumberBox : TextBox {
        //public double MinValue {
        //    get { return (double)GetValue(MinValueProperty); }
        //    set { SetValue(MinValueProperty, value); }
        //}
        //public static readonly DependencyProperty MinValueProperty =
        //    DependencyProperty.Register("MinValue", typeof(double), typeof(ArcTextNumberBox), new PropertyMetadata(0));

        //public double MaxValue {
        //    get { return (double)GetValue(MaxValueProperty); }
        //    set { SetValue(MaxValueProperty, value); }
        //}
        //public static readonly DependencyProperty MaxValueProperty =
        //    DependencyProperty.Register("MaxValue", typeof(double), typeof(ArcTextNumberBox), new PropertyMetadata(0));

        //public int Digits {
        //    get { return (int)GetValue(DigitsProperty); }
        //    set { SetValue(DigitsProperty, value); }
        //}
        //public static readonly DependencyProperty DigitsProperty =
        //    DependencyProperty.Register("Digits", typeof(int), typeof(ArcTextNumberBox), new PropertyMetadata(-1));

        public ArcTextNumberBox() {
            base.OnApplyTemplate();

            //this.TextChanged += ArcTextValueBox_TextChanged;
        }

        //public double GetValue() {
        //    return _value;
        //}

        //public void SetValue(double value) {
        //    _value = value;
        //    this.Text = Digits > -1 ? Math.Round(_value, Digits).ToString() : _value.ToString();
        //}

        //public bool IsFocused() {
        //    return this.FocusState != FocusState.Unfocused;
        //}

        //private void ArcTextValueBox_TextChanged(object sender, TextChangedEventArgs e) {
        //    int selectionStart = this.SelectionStart;
        //    string originalText = this.Text;

        //    // 去除前导零，但保留单独的'0'
        //    string formatText = originalText.TrimStart('0');
        //    if (string.IsNullOrEmpty(formatText)) {
        //        this.Text = "0"; // 如果去除了所有字符，则设置为0
        //        this.SelectionStart = 1; // 保证光标在最后
        //        return;
        //    }
        //    if (formatText[^1] == '.') formatText += '0'; // 如果最后一个字符是'.'，则添加'0'

        //    if (!IsValid(formatText, out double val)) {
        //        // 如果解析失败或值不在范围内，则恢复旧值
        //        this.Text = Math.Round(_value, Digits).ToString();
        //    }
        //    else {
        //        // 更新内部值
        //        _value = val;
        //        // 更新显示以确保没有前导零,且保留小数点
        //        //if (_value.ToString() != formatText) {
        //        this.Text = formatText.ToString();
        //        //}
        //    }

        //    // 恢复光标位置
        //    this.SelectionStart = Math.Max(selectionStart - (this.Text.Length - formatText.Length), 0);
        //}

        //protected override void OnPreviewKeyDown(KeyRoutedEventArgs e) {
        //    if (!IsValid(this.Text, out double _)) {
        //        e.Handled = true;
        //    }
        //}

        //private bool IsValid(string s, out double val) {
        //    return double.TryParse(
        //        s.Length == 0 ? "0" : this.Text,
        //        NumberStyles.AllowDecimalPoint,
        //        CultureInfo.InvariantCulture,
        //        out val)
        //        && val >= MinValue && val <= MaxValue;
        //}

        //private double _value;
    }
}
