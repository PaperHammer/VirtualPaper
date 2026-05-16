using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Events.EffectValue;
using VirtualPaper.Common.Events.EffectValue.Base;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.PlayerWeb.Core.Interfaces;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Components.General {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GeneralEffect : Page, IDisposable {
        public GeneralEffect() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            if (e.Parameter is FrameworkPayload payload) {
                payload.TryGet(NaviPayloadKey.IEffectService.ToString(), out _effectService);
                payload.TryGet(NaviPayloadKey.StartArgs.ToString(), out _startArgs);
                payload.TryGet(NaviPayloadKey.ApplyService.ToString(), out _applyService);
            }
        }

        private void MainGrid_Loaded(object sender, RoutedEventArgs e) {
            if (_isInitialized) return;

            _isInitialized = true;
            InitUI();
        }

        private void InitUI() {
            skPanel.Children.Clear();
            if (AnyFilePathsEmpty()) {
                return;
            }

            _wpEffectData = JsonNodeUtil.GetWritableJson(_startArgs.WpEffectFilePathUsing);
            GenerateUIElements();
        }

        #region generate ui
        private void GenerateUIElements() {
            if (_wpEffectData == null) return;

            UIElement? obj = null;
            foreach (var item in _wpEffectData.AsObject()) {
                string uiElementType = item.Value["Type"].ToString();
                if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
                    Slider slider;
                    if (_controls.TryGetValue(item.Key, out UIElement? value)) {
                        slider = value as Slider;
                        slider.Value = (double)item.Value["Value"];
                    }
                    else {
                        slider = new Slider() {
                            Name = item.Key,
                            MinWidth = MIN_WIDTH,
                            Margin = MARGIN,
                            Maximum = (double)item.Value["Max"],
                            Minimum = (double)item.Value["Min"],
                            Value = (double)item.Value["Value"],
                        };
                        var stepJsonNode = item.Value["Step"];
                        if (stepJsonNode != null) {
                            slider.StepFrequency = double.Parse(stepJsonNode.ToString());
                        }
                        slider.ValueChanged += Slider_ValueChanged;
                    }
                    obj = slider;
                }
                else if (uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
                    TextBox tb;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        tb = value as TextBox;
                        tb.Text = item.Value["Value"].ToString();
                    }
                    else {
                        tb = new TextBox {
                            Name = item.Key,
                            Text = item.Value["Value"].ToString(),
                            AcceptsReturn = true,
                            MaxWidth = MIN_WIDTH,
                            MinWidth = MIN_WIDTH,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = MARGIN
                        };
                        tb.TextChanged += Textbox_TextChanged;
                    }
                    obj = tb;
                }
                else if (uiElementType.Equals("CheckBox", StringComparison.OrdinalIgnoreCase)) {
                    CheckBox chk;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        chk = value as CheckBox;
                        chk.IsChecked = (bool)item.Value["Value"];
                        chk.Content = item.Value["Text"].ToString();
                    }
                    else {
                        chk = new CheckBox {
                            Name = item.Key,
                            Content = item.Value["Text"].ToString(),
                            IsChecked = (bool)item.Value["Value"],
                            HorizontalAlignment = HorizontalAlignment.Left,
                            MinWidth = MIN_WIDTH,
                            Margin = MARGIN
                        };
                        var helpJsonNode = item.Value["Help"];
                        if (helpJsonNode != null) {
                            ToolTipService.SetToolTip(
                                chk,
                                new ToolTip() { Content = LanguageUtil.GetI18n(helpJsonNode.ToString()), Visibility = Visibility.Visible });
                        }
                        chk.Checked += Checkbox_CheckedChanged;
                        chk.Unchecked += Checkbox_CheckedChanged;
                    }
                    obj = chk;
                }
                else if (uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
                    ComboBox cmbBox;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        cmbBox = value as ComboBox;
                        cmbBox.SelectedIndex = (int)item.Value["Value"];
                    }
                    else {
                        cmbBox = new ComboBox() {
                            Name = item.Key,
                            MinWidth = MIN_WIDTH,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = MARGIN,
                            SelectedIndex = (int)item.Value["Value"],
                        };
                        var array = item.Value["Items"].AsArray();
                        foreach (var dropItem in array) {
                            cmbBox.Items.Add(dropItem.ToString());
                        }
                        cmbBox.SelectionChanged += ComboBox_SelectionChanged;
                    }
                    obj = cmbBox;
                }
                else if (uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
                    TextBlock label;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        label = value as TextBlock;
                        label.Text = item.Value["Value"].ToString();
                    }
                    else {
                        label = new TextBlock {
                            Name = item.Key,
                            Text = item.Value["Value"].ToString(),
                            MinWidth = MIN_WIDTH,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = MARGIN
                        };
                        var helpJsonNode = item.Value["Help"];
                        if (helpJsonNode != null) {
                            ToolTipService.SetToolTip(
                                label,
                                new ToolTip() { Content = LanguageUtil.GetI18n(helpJsonNode.ToString()), Visibility = Visibility.Visible });
                        }
                    }
                    obj = label;
                }
                else {
                    continue;
                }

                // Title for Slider, ComboBox..
                var textJsonNode = item.Value["Text"];
                if (textJsonNode != null &&
                    !uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase) &&
                    !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {

                    var title = textJsonNode.ToString();
                    TextBlock tb;
                    if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
                        tb = SetSliderExtra(item, title);
                    }
                    else {
                        tb = new TextBlock {
                            Text = title,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            MinWidth = MIN_WIDTH,
                        };
                        AddUIElement(item.Key + "_Title", tb, false);
                    }

                    var helpJsonNode = item.Value["Help"];
                    if (helpJsonNode != null) {
                        ToolTipService.SetToolTip(
                            tb,
                            new ToolTip { Content = LanguageUtil.GetI18n(helpJsonNode.ToString()), Visibility = Visibility.Visible });
                    }
                }

                AddUIElement(item.Key, obj, true);
            }
        }

        private TextBlock SetSliderExtra(KeyValuePair<string, JsonNode?> item, string title) {
            var valueText = new TextBlock {
                Name = item.Key + "_Value",
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = ((double)item.Value["Value"]).ToString("0.#"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };
            _controls[item.Key + "_Value"] = valueText;

            var grid = new Grid {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = MIN_WIDTH
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tb = new TextBlock {
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, 0);
            Grid.SetColumn(valueText, 1);

            grid.Children.Add(tb);
            grid.Children.Add(valueText);

            AddUIElement(item.Key + "_Title", grid, false);

            return tb;
        }

        private void AddUIElement(string name, UIElement obj, bool needTrack) {
            skPanel.Children.Add(obj);
            if (needTrack) {
                _controls[name] = obj;
            }
        }

        private void UnSubscribe() {
            foreach (var kvp in _controls) {
                var control = kvp.Value;
                if (control is Slider slider) {
                    slider.ValueChanged -= Slider_ValueChanged;
                }
                else if (control is TextBox tb) {
                    tb.TextChanged -= Textbox_TextChanged;
                }
                else if (control is CheckBox chk) {
                    chk.Checked -= Checkbox_CheckedChanged;
                    chk.Unchecked -= Checkbox_CheckedChanged;
                }
                else if (control is ComboBox cmbBox) {
                    cmbBox.SelectionChanged -= ComboBox_SelectionChanged;
                }
            }
            _controls.Clear();
        }
        #endregion

        #region ui event
        #region slider
        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs? e = null) {
            try {
                var item = (Slider)sender;
                _wpEffectData[item.Name]["Value"] = item.Value;

                string valueKey = item.Name + "_Value";
                if (_controls.TryGetValue(valueKey, out UIElement valueTb)) {
                    ((TextBlock)valueTb).Text = item.Value.ToString("0.#");
                }

                OnEffectValueChanged(new EffectValueChanged<double> { ControlName = "Slider", PropertyName = item.Name, Value = (double)item.Value });
            }
            catch { }
        }
        #endregion

        #region dropdown
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e = default) {
            try {
                var item = (ComboBox)sender;
                _wpEffectData[item.Name]["Value"] = item.SelectedIndex;
                OnEffectValueChanged(new EffectValueChanged<int> { ControlName = "Dropdown", PropertyName = item.Name, Value = item.SelectedIndex });
            }
            catch { }
        }

        //private void FolderCmbBox_SelectionChanged(object sender, SelectionChangedEventArgs ex)
        //{
        //    try
        //    {
        //        var menuItem = (ComboBox)sender;
        //        var propertyName = (menuItem.Parent as StackPanel).Name;
        //        var filePath = Path.Combine(_wpEffectData[propertyName]["Folder"].ToString(), menuItem.SelectedItem.ToString()); //filename is unique.
        //        WallpaperSendMsg(new VirtualPaperFolderDropdown() { Name = propertyName, Value = filePath });
        //        _wpEffectData[propertyName]["Value"] = menuItem.SelectedItem.ToString();
        //        //UpdatePropertyFile();
        //    }
        //    catch { }
        //}

        //private async void FolderDropDownOpenFileBtn_Click(object sender, RoutedEventArgs ex)
        //{
        //    try
        //    {
        //        var btn = sender as Button;
        //        //find folder selection ComboBox
        //        var panel = btn.Parent as StackPanel;
        //        var cmbBox = panel.Children[0] as ComboBox;

        //        foreach (var lp in _wpEffectData)
        //        {
        //            string uiElementType = lp.Value["Type"].ToString();
        //            if (uiElementType.Equals("folderDropdown", StringComparison.OrdinalIgnoreCase) && panel.Name == lp.Key)
        //            {
        //                var filePicker = new FileOpenPicker();
        //                var filterString = lp.Value["Filter"].ToString();
        //                var filter = filterString == "*" ? new string[] { "*" } : filterString.Replace("*", string.Empty).Split("|");
        //                foreach (var item in filter)
        //                {
        //                    filePicker.FileTypeFilter.Block(item);
        //                }
        //                var selectedFiles = await filePicker.PickMultipleFilesAsync();
        //                if (selectedFiles.Count > 0)
        //                {
        //                    var destFiles = new List<string>();
        //                    var destFolder = Path.Combine(Path.GetDirectoryName(_runtimeData.FilePath), lp.Value["folder"].ToString());
        //                    //copy the new file over..
        //                    foreach (var srcFile in selectedFiles)
        //                    {
        //                        var destFile = Path.Combine(destFolder, Path.GetFileName(srcFile.Path));
        //                        if (!File.Exists(destFile))
        //                        {
        //                            File.Copy(srcFile.Path, destFile);
        //                        }
        //                        else
        //                        {
        //                            destFile = FileUtil.NextAvailableFilename(destFile);
        //                            File.Copy(srcFile.Path, destFile);
        //                        }
        //                        destFiles.Block(Path.GetFileName(destFile));
        //                    }
        //                    destFiles.Sort();
        //                    //add copied files to bottom of dropdown..
        //                    foreach (var file in destFiles)
        //                    {
        //                        cmbBox.Items.Block(file);
        //                    }

        //                    if (selectedFiles.Count == 1)
        //                    {
        //                        cmbBox.SelectedIndex = cmbBox.Items.Count - 1;
        //                    }
        //                }
        //                break;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        App.Log.Error(ex);
        //    }
        //}

        //private static string[] GetFileNames(string path, string searchPattern, SearchOption searchOption)
        //{
        //    string[] searchPatterns = searchPattern.Split('|');
        //    List<string> files = [];
        //    foreach (string sp in searchPatterns)
        //        files.AddRange(System.IO.Directory.GetFiles(path, sp, searchOption));
        //    files.Sort();

        //    List<string> tmp = [];
        //    foreach (var item in files)
        //    {
        //        tmp.Block(Path.GetFileName(item));
        //    }
        //    return tmp.ToArray();
        //}
        #endregion

        #region color picker
        //private void ColorPicker_ColorChanged(Microsoft.UI.Xaml.Controls.ColorPicker sender, ColorChangedEventArgs args)
        //{
        //    try
        //    {
        //        var panel = sender.Tag as StackPanel;
        //        var colorPickerBtn = panel.Children[0] as ColorPickerButton;
        //        colorPickerBtn.SelectedColor = Color.FromArgb(
        //            255,
        //            args.NewColor.R,
        //            args.NewColor.G,
        //            args.NewColor.B
        //        );

        //        WallpaperSendMsg(new VirtualPaperColorPicker() { Name = panel.Name, Value = ToHexValue(args.NewColor) });
        //        _wpEffectData[panel.Name]["Value"] = ToHexValue(args.NewColor);
        //        //UpdatePropertyFile();
        //    }
        //    catch (Exception ex)
        //    {
        //        App.Log.Error(ex);
        //    }
        //}

        //public static Color GetColorAt(int x, int y)
        //{
        //    IntPtr desk = Native.GetDesktopWindow();
        //    IntPtr dc = Native.GetWindowDC(desk);
        //    try
        //    {
        //        int a = (int)Native.GetPixel(dc, x, y);
        //        return Color.FromArgb(255, (byte)((a >> 0) & 0xff), (byte)((a >> 8) & 0xff), (byte)((a >> 16) & 0xff));
        //    }
        //    finally
        //    {
        //        Native.ReleaseDC(desk, dc);
        //    }
        //}

        //private static string ToHexValue(Color color)
        //{
        //    return "#" + color.R.ToString("X2") +
        //                 color.G.ToString("X2") +
        //                 color.B.ToString("X2");
        //}

        //public SolidColorBrush GetSolidColorBrush(string hexaColor)
        //{
        //    return new SolidColorBrush(Color.FromArgb(
        //            255,
        //            Convert.ToByte(hexaColor.Substring(1, 2), 16),
        //            Convert.ToByte(hexaColor.Substring(3, 2), 16),
        //            Convert.ToByte(hexaColor.Substring(5, 2), 16)
        //        ));
        //}
        #endregion

        #region button
        private void RestoretBtn_Click(object sender, RoutedEventArgs e) {
            if (AnyFilePathsEmpty()) {
                return;
            }

            btnRestore.IsEnabled = false;

            File.Copy(_startArgs.WpEffectFilePathUsing, _startArgs.WpEffectFilePathTemporary, true);
            InitUI();

            btnRestore.IsEnabled = true;
        }

        private void SaveAndApplyBtn_Click(object sender, RoutedEventArgs e) {
            UpdatePropertyFile(true);
            _applyService?.OnApply(null);
        }
        #endregion

        #region checkbox
        private void Checkbox_CheckedChanged(object sender, RoutedEventArgs e = default) {
            try {
                var item = (CheckBox)sender;
                _wpEffectData[item.Name]["Value"] = item.IsChecked == true;
                OnEffectValueChanged(new EffectValueChanged<bool> { ControlName = "Checkbox", PropertyName = item.Name, Value = (bool)item.IsChecked });
            }
            catch { }
        }
        #endregion

        #region textbox
        private void Textbox_TextChanged(object sender, TextChangedEventArgs e = default) {
            try {
                var item = (TextBox)sender;
                _wpEffectData[item.Name]["Value"] = item.Text;
                OnEffectValueChanged(new EffectValueChanged<string> { ControlName = "Textbox", PropertyName = item.Name, Value = item.Text });
            }
            catch { }
        }
        #endregion
        #endregion

        #region value changed        
        private void OnEffectValueChanged<T>(EffectValueChanged<T> e) {
            _effectService?.UpdateEffectValue(e);
        }
        #endregion

        #region utils
        private void UpdatePropertyFile(bool isSave) {
            if (isSave) {
                JsonNodeUtil.Write(_startArgs.WpEffectFilePathUsing, _wpEffectData);
            }
        }

        private bool AnyFilePathsEmpty() {
            return _effectService == null ||
                _startArgs == null ||
                _startArgs.WpEffectFilePathUsing == string.Empty;
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        private void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    UnSubscribe();
                }
                _controls.Clear();
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private Thickness MARGIN = new Thickness(0, 4, 0, 12);
        private const double MIN_WIDTH = 200;
        private JsonNode? _wpEffectData;
        private StartArgsWeb _startArgs = null!;
        private IEffectService _effectService = null!;
        private readonly Dictionary<string, UIElement> _controls = [];
        private bool _isInitialized;
        private IApplyService _applyService;
    }
}
