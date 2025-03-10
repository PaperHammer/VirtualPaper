using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VirtualPaper.Common;
using VirtualPaper.Common.Events.EffectValue;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.UIComponent.Utils;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Data {
    public sealed partial class EffectConfig : UserControl, IDisposable {
        public event EventHandler<DoubleValueChangedEventArgs> DoubleValueChanged;
        public event EventHandler<IntValueChangedEventArgs> IntValueChanged;
        public event EventHandler<BoolValueChangedEventArgs> BoolValueChanged;
        public event EventHandler<StringValueChangedEventArgs> StringValueChanged;
        public event EventHandler SaveAndApply;

        #region init
        public EffectConfig(
            string wpEffectFilePathUsing,
            string wpEffectFilePathTemporary,
            string wpEffectFilePathTemplate) {
            _wpEffectFilePathUsing = wpEffectFilePathUsing;
            _wpEffectFilePathTemporary = wpEffectFilePathTemporary;
            _wpEffectFilePathTemplate = wpEffectFilePathTemplate;
             _controls = [];
            _localizer = LanguageUtil.LocalizerInstacne;

            this.InitializeComponent();           

            InitText();
            InitUI();
        }

        public void Closing(object sender, EventArgs e) {
            RestoretBtn_Click(sender, default);
        }

        private void InitText() {            
            _textRestore = _localizer.GetLocalizedString(Constants.I18n.Text_Restore);
            _textSaveAndApply = _localizer.GetLocalizedString(Constants.I18n.Text_SaveAndApply);
        }

        private void InitUI() {
            skPanel.Children.Clear();
            if (AnyFileIPathsEmpty()) {
                return;
            }

            File.Copy(_wpEffectFilePathUsing, _wpEffectFilePathTemporary, true);
            _wpEffectData = JsonNodeUtil.GetWritableJson(_wpEffectFilePathTemporary);
            GenerateUIElements();
        }

        private void GenerateUIElements() {
            if (_wpEffectData == null) return;

            UIElement obj = null;
            foreach (var item in _wpEffectData.AsObject()) {
                string uiElementType = item.Value["Type"].ToString();
                if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
                    Slider slider = null;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        slider = value as Slider;
                        slider.Value = (double)item.Value["Value"];
                    }
                    else {
                        slider = new Slider() {
                            Name = item.Key,
                            MinWidth = _minWidth,
                            Margin = _margin,
                            Maximum = (double)item.Value["Max"],
                            Minimum = (double)item.Value["Min"],
                            Value = (double)item.Value["Value"],
                        };
                        var stepJsonNode = item.Value["Step"];
                        if (stepJsonNode != null) {
                            slider.StepFrequency = double.Parse(stepJsonNode.ToString());
                        }
                        var helpJsonNode = item.Value["Help"];
                        if (helpJsonNode != null) {
                            ToolTipService.SetToolTip(
                                slider,
                                new ToolTip() { Content = _localizer.GetLocalizedString(helpJsonNode.ToString()) });
                        }
                        slider.ValueChanged += Slider_ValueChanged;
                    }
                    Slider_ValueChanged(slider);
                    obj = slider;
                }
                else if (uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
                    TextBox tb = null;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        tb = value as TextBox;
                        tb.Text = item.Value["Value"].ToString();
                    }
                    else {
                        tb = new TextBox {
                            Name = item.Key,
                            Text = item.Value["Value"].ToString(),
                            AcceptsReturn = true,
                            MaxWidth = _minWidth,
                            MinWidth = _minWidth,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = _margin
                        };
                        var helpJsonNode = item.Value["Help"];
                        if (helpJsonNode != null) {
                            ToolTipService.SetToolTip(
                                tb,
                                new ToolTip() { Content = _localizer.GetLocalizedString(helpJsonNode.ToString()) });
                        }
                        tb.TextChanged += Textbox_TextChanged;
                    }
                    Textbox_TextChanged(tb);
                    obj = tb;
                }
                else if (uiElementType.Equals("CheckBox", StringComparison.OrdinalIgnoreCase)) {
                    CheckBox chk = null;
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
                            MinWidth = _minWidth,
                            Margin = _margin
                        };
                        var helpJsonNode = item.Value["Help"];
                        if (helpJsonNode != null) {
                            ToolTipService.SetToolTip(
                                chk,
                                new ToolTip() { Content = _localizer.GetLocalizedString(helpJsonNode.ToString()) });
                        }
                        chk.Checked += Checkbox_CheckedChanged;
                        chk.Unchecked += Checkbox_CheckedChanged;
                    }
                    Checkbox_CheckedChanged(chk);
                    obj = chk;
                }
                else if (uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
                    ComboBox cmbBox = null;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        cmbBox = value as ComboBox;
                        cmbBox.SelectedIndex = (int)item.Value["Value"];
                    }
                    else {
                        cmbBox = new ComboBox() {
                            Name = item.Key,
                            MinWidth = _minWidth,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = _margin,
                            SelectedIndex = (int)item.Value["Value"],
                        };
                        var array = item.Value["Items"].AsArray();
                        foreach (var dropItem in array) {
                            cmbBox.Items.Add(dropItem.ToString());
                        }
                        var helpJsonNode = item.Value["Help"];
                        if (helpJsonNode != null) {
                            ToolTipService.SetToolTip(
                                cmbBox,
                                new ToolTip() { Content = _localizer.GetLocalizedString(helpJsonNode.ToString()) });
                        }
                        cmbBox.SelectionChanged += ComboBox_SelectionChanged;
                    }
                    ComboBox_SelectionChanged(cmbBox);
                    obj = cmbBox;
                }
                else if (uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
                    TextBlock label = null;
                    if (_controls.TryGetValue(item.Key, out UIElement value)) {
                        label = value as TextBlock;
                        label.Text = item.Value["Value"].ToString();
                    }
                    else {
                        label = new TextBlock {
                            Name = item.Key,
                            Text = item.Value["Value"].ToString(),
                            MinWidth = _minWidth,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = _margin
                        };
                    }
                    obj = label;
                }
                else {
                    continue;
                }

                //Title for Slider, ComboBox..
                var textJsonNode = item.Value["Text"];
                if (textJsonNode != null &&
                    !uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase) &&
                    !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
                    var text = textJsonNode.ToString();
                    var tb = new TextBlock {
                        Text = text,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MinWidth = _minWidth,
                        Margin = _margin
                    };
                    AddUIElement(text, tb, false);
                    var helpJsonNode = item.Value["Help"];
                    if (helpJsonNode != null) {
                        ToolTipService.SetToolTip(
                            tb,
                            new ToolTip() { Content = _localizer.GetLocalizedString(helpJsonNode.ToString()) });
                    }
                }

                AddUIElement(item.Key, obj, true);
            }
        }

        private void AddUIElement(string name, UIElement obj, bool needTrack) {
            skPanel.Children.Add(obj);
            if (needTrack) {
                _controls[name] = obj;
            }
        }

        private void RemoveAllUIElement() {
            skPanel.Children.Clear();
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

        #region slider
        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e = default) {
            try {
                var item = (Slider)sender;
                _wpEffectData[item.Name]["Value"] = item.Value;
                OnEffectValueChanged(new DoubleValueChangedEventArgs { ControlName = "Slider", PropertyName = item.Name, Value = (double)item.Value });
            }
            catch { }
        }
        #endregion

        #region dropdown
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e = default) {
            try {
                var item = (ComboBox)sender;
                _wpEffectData[item.Name]["Value"] = item.SelectedIndex;
                OnEffectValueChanged(new IntValueChangedEventArgs { ControlName = "Dropdown", PropertyName = item.Name, Value = item.SelectedIndex });
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
        //                    filePicker.FileTypeFilter.Add(item);
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
        //                        destFiles.Add(Path.GetFileName(destFile));
        //                    }
        //                    destFiles.Sort();
        //                    //add copied files to bottom of dropdown..
        //                    foreach (var file in destFiles)
        //                    {
        //                        cmbBox.Items.Add(file);
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
        //        tmp.Add(Path.GetFileName(item));
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
        private async void RestoretBtn_Click(object sender, RoutedEventArgs e) {
            if (AnyFileIPathsEmpty()) {
                return;
            }

            btnRestore.IsEnabled = false;

            File.Copy(_wpEffectFilePathTemplate, _wpEffectFilePathTemporary, true);
            RemoveAllUIElement();
            InitUI();

            await Task.Delay(1000);
            btnRestore.IsEnabled = true;
        }

        private void SaveAndApplyBtn_Click(object sender, RoutedEventArgs e) {
            UpdatePropertyFile(true);
            SaveAndApply?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region checkbox
        private void Checkbox_CheckedChanged(object sender, RoutedEventArgs e = default) {
            try {
                var item = (CheckBox)sender;
                _wpEffectData[item.Name]["Value"] = item.IsChecked == true;
                OnEffectValueChanged(new BoolValueChangedEventArgs { ControlName = "Checkbox", PropertyName = item.Name, Value = (bool)item.IsChecked });
            }
            catch { }
        }
        #endregion

        #region textbox
        private void Textbox_TextChanged(object sender, TextChangedEventArgs e = default) {
            try {
                var item = (TextBox)sender;
                _wpEffectData[item.Name]["Value"] = item.Text;
                OnEffectValueChanged(new StringValueChangedEventArgs { ControlName = "Textbox", PropertyName = item.Name, Value = item.Text });
            }
            catch { }
        }
        #endregion

        public void UpdatePropertyFile(bool isSave) {
            if (isSave) {
                JsonNodeUtil.Write(_wpEffectFilePathUsing, _wpEffectData);
            }
            JsonNodeUtil.Write(_wpEffectFilePathTemporary, _wpEffectData);
        }

        private bool AnyFileIPathsEmpty() {
            return _wpEffectFilePathUsing == string.Empty ||
                _wpEffectFilePathTemporary == string.Empty ||
                _wpEffectFilePathTemplate == string.Empty;
        }

        //private void EyeDropBtn_Click(object sender, RoutedEventArgs ex)
        //{
        //    var stackPanel = (sender as Button).Parent as StackPanel;
        //    var colorPickerBtn = stackPanel.Children[0] as ColorPickerButton;

        //    var eyeDropper = new ColorEyeDropWindow();
        //    eyeDropper.Activate();
        //    eyeDropper.Closed += (_, _) =>
        //    {
        //        if (eyeDropper.SelectedColor != null && colorPickerBtn != null)
        //        {
        //            this.DispatcherQueue.TryEnqueue(() => {
        //                var color = (Color)eyeDropper.SelectedColor;
        //                //not updating colorpicker
        //                //colorPickerBtn.SelectedColor = color;
        //                colorPickerBtn.ColorPicker.Color = color;
        //            });
        //        }
        //    };
        //}

        //internal async void SendMessage(object sender, EventArgs args) {
        //    var monitor = _wpSettingsViewModel.GetSelectedMonitor();

        //    foreach (var item in _wpEffectData.AsObject()) {
        //        string uiElementType = item.Value["Type"].ToString();
        //        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
        //            await _wpControlClient.SendMessageWallpaperAsync(monitor, Metadata.RuntimeData, new VirtualPaperSlider() {
        //                Name = item.Key,
        //                Value = (double)item.Value["Value"]
        //            });
        //        }
        //        else if (uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
        //            await _wpControlClient.SendMessageWallpaperAsync(monitor, Metadata.RuntimeData, new VirtualPaperTextBox() {
        //                Name = item.Key,
        //                Value = item.Value["Value"].ToString(),
        //            });
        //        }
        //        else if (uiElementType.Equals("CheckBox", StringComparison.OrdinalIgnoreCase)) {
        //            await _wpControlClient.SendMessageWallpaperAsync(monitor, Metadata.RuntimeData, new VirtualPaperCheckbox() {
        //                Name = item.Key,
        //                Value = (bool)item.Value["Value"],
        //            });
        //        }
        //        else if (uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
        //            await _wpControlClient.SendMessageWallpaperAsync(monitor, Metadata.RuntimeData, new VirtualPaperDropdown() {
        //                Name = item.Key,
        //                Value = (int)item.Value["Value"],
        //            });
        //        }
        //    }

        //    await _wpControlClient.SendMessageWallpaperAsync(monitor, Metadata.RuntimeData, new VirtualPaperApplyCmd());
        //}

        #region value changed        
        private void OnEffectValueChanged(DoubleValueChangedEventArgs e) {
            UpdatePropertyFile(false);
            DoubleValueChanged?.Invoke(this, e);
        }
        private void OnEffectValueChanged(IntValueChangedEventArgs e) {
            UpdatePropertyFile(false);
            IntValueChanged?.Invoke(this, e);
        }

        private void OnEffectValueChanged(BoolValueChangedEventArgs e) {
            UpdatePropertyFile(false);
            BoolValueChanged?.Invoke(this, e);
        }

        private void OnEffectValueChanged(StringValueChangedEventArgs e) {
            UpdatePropertyFile(false);
            StringValueChanged?.Invoke(this, e);
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        private void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {                    
                    DoubleValueChanged = null;
                    IntValueChanged = null;
                    BoolValueChanged = null;
                    StringValueChanged = null;
                    UnSubscribe();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly string _wpEffectFilePathUsing;
        private readonly string _wpEffectFilePathTemporary;
        private readonly string _wpEffectFilePathTemplate;
        private JsonNode _wpEffectData;
        private readonly Thickness _margin = new(0, 0, 20, 10);
        private const double _minWidth = 200;
        private readonly ILocalizer _localizer;
        private string _textRestore;
        private string _textSaveAndApply;
        private readonly Dictionary<string, UIElement> _controls;
    }
}
