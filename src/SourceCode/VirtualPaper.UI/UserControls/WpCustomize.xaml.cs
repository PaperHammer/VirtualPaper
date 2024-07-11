using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using VirtualPaper.Common;
using VirtualPaper.Common.Models;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.ObserverMode;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.UserControls
{
    public sealed partial class WpCustomize : UserControl, IDisposable
    {
        #region init
        private WpCustomize()
        {
            this.InitializeComponent();

            _userSettings = App.Services.GetRequiredService<IUserSettingsClient>();
            _wpControl = App.Services.GetRequiredService<IWallpaperControlClient>();
            _monitorManager = App.Services.GetRequiredService<IMonitorManagerClient>();
            //_dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueueController.CreateOnCurrentThread().DispatcherQueue;            
        }

        public WpCustomize(IMetaData metaData) : this() // import to library
        {
            var wpInfo = GetWpCustomizeDetails(metaData, _userSettings.Settings.WallpaperArrangement, _userSettings.Settings.SelectedMonitor);

            metaData.WpCustomizePathUsing = wpInfo.Item1;
            metaData.WpCustomizePathTmp = Path.Combine(metaData.FolderPath, "WpCustomizePathTmp.json");
            if (!File.Exists(metaData.WpCustomizePathTmp))
            {
                File.Copy(metaData.WpCustomizePath, metaData.WpCustomizePathTmp, true);
            }
        }

        public WpCustomize(
            IMonitor monitor,
            IMetaData metaData,
            EventHandler<IntValueChangedEventArgs> intValueChanged,
            EventHandler<DoubleValueChangedEventArgs> doubleValueChanged,
            EventHandler<BoolValueChangedEventArgs> boolValueChanged,
            EventHandler<StringValueChangedEventArgs> stringValueChanged) : this()
        {
            _intValueChanged = intValueChanged;
            _doubleValueChanged = doubleValueChanged;
            _boolValueChanged = boolValueChanged;
            _stringValueChanged = stringValueChanged;

            _monitor = monitor;
            _metaData = metaData;
            try
            {
                var wpInfo = GetWpCustomizeDetails(metaData, _userSettings.Settings.WallpaperArrangement, _userSettings.Settings.SelectedMonitor);

                metaData.WpCustomizePathUsing = wpInfo.Item1;
                metaData.WpCustomizePathTmp = Path.Combine(metaData.FolderPath, "WpCustomizePathTmp.json");
                if (!File.Exists(metaData.WpCustomizePathTmp))
                {
                    File.Copy(metaData.WpCustomizePath, metaData.WpCustomizePathTmp, true);
                }

                this._wpCustomizePathUsing = metaData.WpCustomizePathUsing;
                this._wpCustomizePathTmp = metaData.WpCustomizePathTmp;
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
                return;
            }
            ReadUI();
        }

        private async void ReadUI()
        {
            try
            {
                if (_wpCustomizePathTmp != null)
                {
                    this._wpCustomizeData = JsonUtil.ReadJObject(_wpCustomizePathTmp);
                }
                GenerateUIElements();
            }
            catch (Exception)
            {
                //BtnRestoreDefault.Visibility = Visibility.Collapsed;

                _logger.Info($"{_wpCustomizePathTmp} read failed, restoring...");

                await App.Services.GetRequiredService<IWallpaperControlClient>().ResetWpCustomizeAsync(
                    _wpCustomizePathTmp,
                    (Grpc.Service.WallpaperControl.WallpaperType)_metaData.Type);
                this._wpCustomizeData = JsonUtil.ReadJObject(_wpCustomizePathTmp);
                GenerateUIElements();
            }
        }

        private void GenerateUIElements()
        {
            if (_wpCustomizeData == null)
            {
                var msg = "wpCustomizePathUsing file not found!";
                //Empty..
                AddUIElement(new TextBlock
                {
                    Text = msg,
                    //Background = Brushes.Red,
                    FontSize = 18,
                    //Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 50, 0, 0)
                });
                BtnRestoreDefault.IsEnabled = false;
                return;
            }
            else if (_wpCustomizeData.Count == 0)
            {
                //Empty..
                AddUIElement(new TextBlock
                {
                    Text = "El Psy Congroo",
                    //Foreground = Brushes.Yellow,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = _margin
                });
                BtnRestoreDefault.IsEnabled = false;
                return;
            }

            var wpType = _metaData.Type;

            UIElement obj = null;
            foreach (var item in _wpCustomizeData)
            {
                string uiElementType = item.Value["Type"].ToString();
                if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase))
                {
                    var slider = new Slider()
                    {
                        Name = item.Key,
                        MinWidth = _minWidth,
                        Margin = _margin,
                        Maximum = (double)item.Value["Max"],
                        Minimum = (double)item.Value["Min"],
                        Value = (double)item.Value["Value"],
                    };
                    if (item.Value["Step"] != null && !string.IsNullOrWhiteSpace(item.Value["Step"].ToString()))
                    {
                        slider.StepFrequency = (double)item.Value["Step"];
                    }
                    if (item.Value["Help"] != null && !string.IsNullOrWhiteSpace(item.Value["Help"].ToString()))
                    {
                        ToolTipService.SetToolTip(slider, new ToolTip() { Content = (string)item.Value["Help"] });
                    }
                    slider.ValueChanged += Slider_ValueChanged;
                    Slider_ValueChanged(slider);
                    obj = slider;
                }
                else if (uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase))
                {
                    var tb = new TextBox
                    {
                        Name = item.Key,
                        Text = item.Value["Value"].ToString(),
                        AcceptsReturn = true,
                        MaxWidth = _minWidth,
                        MinWidth = _minWidth,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = _margin
                    };
                    if (item.Value["Help"] != null && !string.IsNullOrWhiteSpace(item.Value["Help"].ToString()))
                    {
                        ToolTipService.SetToolTip(tb, new ToolTip() { Content = (string)item.Value["Help"] });
                    }
                    tb.TextChanged += Textbox_TextChanged;
                    Textbox_TextChanged(tb);
                    obj = tb;
                }                
                else if (uiElementType.Equals("CheckBox", StringComparison.OrdinalIgnoreCase))
                {
                    var chk = new CheckBox
                    {
                        Name = item.Key,
                        Content = item.Value["Text"].ToString(),
                        IsChecked = (bool)item.Value["Value"],
                        HorizontalAlignment = HorizontalAlignment.Left,
                        //MaxWidth = _minWidth,
                        MinWidth = _minWidth,
                        Margin = _margin
                    };
                    if (item.Value["Help"] != null && !string.IsNullOrWhiteSpace(item.Value["Help"].ToString()))
                    {
                        ToolTipService.SetToolTip(chk, new ToolTip() { Content = (string)item.Value["Help"] });
                    }
                    chk.Checked += Checkbox_CheckedChanged;
                    chk.Unchecked += Checkbox_CheckedChanged;
                    Checkbox_CheckedChanged(chk);
                    obj = chk;
                }
                else if (uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase))
                {
                    var cmbBox = new ComboBox()
                    {
                        Name = item.Key,
                        //MaxWidth = _minWidth,
                        MinWidth = _minWidth,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = _margin,
                        SelectedIndex = (int)item.Value["Value"],
                    };
                    foreach (var dropItem in item.Value["Items"])
                    {
                        cmbBox.Items.Add(dropItem.ToString());
                    }
                    if (item.Value["Help"] != null && !string.IsNullOrWhiteSpace(item.Value["Help"].ToString()))
                    {
                        ToolTipService.SetToolTip(cmbBox, new ToolTip() { Content = (string)item.Value["Help"] });
                    }
                    cmbBox.SelectionChanged += ComboBox_SelectionChanged;
                    ComboBox_SelectionChanged(cmbBox);
                    obj = cmbBox;
                }
                else if (uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase))
                {
                    var label = new TextBlock
                    {
                        Name = item.Key,
                        Text = item.Value["Value"].ToString(),
                        //MaxWidth = _minWidth,
                        MinWidth = _minWidth,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = _margin
                    };
                    obj = label;
                }
                else
                {
                    continue;
                }

                //Title for Slider, ComboBox..
                if (item.Value["Text"] != null &&
                    !uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase) &&
                    !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase))
                {
                    var tb = new TextBlock
                    {
                        Text = item.Value["Text"].ToString(),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MinWidth = _minWidth,
                        Margin = _margin
                    };
                    AddUIElement(tb);
                    if (item.Value["Help"] != null && !string.IsNullOrWhiteSpace(item.Value["Help"].ToString()))
                    {
                        ToolTipService.SetToolTip(tb, new ToolTip() { Content = (string)item.Value["Help"] });
                    }
                }

                AddUIElement(obj);
            }
        }

        private void AddUIElement(UIElement obj) => skPanel.Children.Add(obj);
        #endregion

        #region slider
        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e = default)
        {
            try
            {
                var item = (Slider)sender;
                //WallpaperSendMsg(new VirtualPaperSlider() { PropertyName = item.PropertyName, Value = item.Value, Step = item.StepFrequency });
                _wpCustomizeData[item.Name]["Value"] = item.Value;
                OnCustomizeValueChanged(new DoubleValueChangedEventArgs { ControlName = "Slider", PropertyName = item.Name, Value = (double)item.Value });
                UpdatePropertyFile(false);
            }
            catch { }
        }
        #endregion

        #region dropdown
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e = default)
        {
            try
            {
                var item = (ComboBox)sender;
                //WallpaperSendMsg(new VirtualPaperDropdown() { Name = item.Name, Value = item.SelectedIndex });
                _wpCustomizeData[item.Name]["Value"] = item.SelectedIndex;
                OnCustomizeValueChanged(new IntValueChangedEventArgs { ControlName = "Dropdown", PropertyName = item.Name, Value = item.SelectedIndex });
                UpdatePropertyFile(false);
            }
            catch { }
        }

        //private void FolderCmbBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    try
        //    {
        //        var menuItem = (ComboBox)sender;
        //        var propertyName = (menuItem.Parent as StackPanel).Name;
        //        var filePath = Path.Combine(_wpCustomizeData[propertyName]["Folder"].ToString(), menuItem.SelectedItem.ToString()); //filename is unique.
        //        WallpaperSendMsg(new VirtualPaperFolderDropdown() { Name = propertyName, Value = filePath });
        //        _wpCustomizeData[propertyName]["Value"] = menuItem.SelectedItem.ToString();
        //        //UpdatePropertyFile();
        //    }
        //    catch { }
        //}

        //private async void FolderDropDownOpenFileBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var btn = sender as Button;
        //        //find folder selection ComboBox
        //        var panel = btn.Parent as StackPanel;
        //        var cmbBox = panel.Children[0] as ComboBox;

        //        foreach (var lp in _wpCustomizeData)
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
        //                    var destFolder = Path.Combine(Path.GetDirectoryName(_metaData.FilePath), lp.Value["folder"].ToString());
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
        //        _logger.Error(ex);
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
        //        _wpCustomizeData[panel.Name]["Value"] = ToHexValue(args.NewColor);
        //        //UpdatePropertyFile();
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.Error(e);
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
        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            lock (_restoreLock)
            {
                BtnRestoreDefault.IsEnabled = false;

                RotateSymbolIcon();

                File.Copy(_metaData.WpCustomizePath, _metaData.WpCustomizePathTmp, true);
                skPanel.Children.Clear();
                ReadUI();

                BtnRestoreDefault.IsEnabled = true;
            }
        }

        private void RotateSymbolIcon()
        {
            // 获取页面资源中的Storyboard
            var storyboard = Resources["RotateStoryboard"] as Storyboard;

            // 确保SymbolIcon有一个初始的RotateTransform，如果还没有的话
            if (SymbolIconElement.RenderTransform is not RotateTransform rotateTransform)
            {
                SymbolIconElement.RenderTransform = new RotateTransform();
            }

            // 开始动画
            storyboard.Begin();
        }

        //private void Btn_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var item = (Button)sender;
        //        //WallpaperSendMsg(new VirtualPaperButton() { Name = item.Name });
        //    }
        //    catch { }
        //}
        #endregion

        #region checkbox
        private void Checkbox_CheckedChanged(object sender, RoutedEventArgs e = default)
        {
            try
            {
                var item = (CheckBox)sender;
                //WallpaperSendMsg(new VirtualPaperCheckbox() { Name = item.Name, Value = (item.IsChecked == true) });
                _wpCustomizeData[item.Name]["Value"] = item.IsChecked == true;
                OnCustomizeValueChanged(new BoolValueChangedEventArgs { ControlName = "Checkbox", PropertyName = item.Name, Value = (bool)item.IsChecked });
                UpdatePropertyFile(false);
            }
            catch { }
        }
        #endregion

        #region textbox
        private void Textbox_TextChanged(object sender, TextChangedEventArgs e = default)
        {
            try
            {
                var item = (TextBox)sender;
                //WallpaperSendMsg(new VirtualPaperTextBox() { Name = item.Name, Value = item.Text });
                _wpCustomizeData[item.Name]["Value"] = item.Text;
                OnCustomizeValueChanged(new StringValueChangedEventArgs { ControlName = "Textbox", PropertyName = item.Name, Value = item.Text });
                UpdatePropertyFile(false);
            }
            catch { }
        }
        #endregion

        #region helpers
        public void UpdatePropertyFile(bool isApply)
        {
            try
            {
                if (isApply) JsonUtil.Write(_wpCustomizePathUsing, _wpCustomizeData);
                JsonUtil.Write(_wpCustomizePathTmp, _wpCustomizeData);
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }
        }

        //private void WallpaperSendMsg(IpcMessage msg)
        //{
        //    _ = _dispatcherQueue.TryEnqueue(() =>
        //    {
        //        switch (_userSettings.Settings.WallpaperArrangement)
        //        {
        //            case WallpaperArrangement.Per:
        //                _wpControl.SendMessageWallpaperAsync(_monitor, _metaData, msg);
        //                break;
        //            case WallpaperArrangement.Expand:
        //            case WallpaperArrangement.Duplicate:
        //                _wpControl.SendMessageWallpaperAsync(_metaData, msg);
        //                break;
        //        }
        //    });
        //}

        ///// <summary>
        ///// Copies WpCustomize.json from root to the Per monitor file.
        ///// </summary>
        ///// <param name="metaData">Wallpaper info.</param>
        ///// <param name="VirtualPaperPropertyCopyPath">Modified WpCustomize.json path.</param>
        ///// <returns></returns>
        //public static bool RestoreCustomize(IMetaData metaData, string wpCustomizePathCopy)
        //{
        //    bool status = false;
        //    try
        //    {
        //        File.Copy(metaData.WpCustomizePath, wpCustomizePathCopy, true);
        //        status = true;
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.Error(e.ToString());
        //    }
        //    return status;
        //}

        /// <summary>
        /// Get WpCustomize.json copy filepath and corresponding _monitor logic.
        /// </summary>
        /// <param name="metaData">wp metaData</param>
        /// <returns></returns>
        public (string, IMonitor) GetWpCustomizeDetails(IMetaData metaData, WallpaperArrangement arrangement, IMonitor selectedMonitor)
        {
            if (metaData.WpCustomizePath == null || metaData.WpCustomizePath == string.Empty)
            {
                throw new ArgumentException("Non-customizable wallpaper.");
            }

            string wpCustomizePathUsing = string.Empty;
            IMonitor monitor = null;
            var items = _wpControl.Wallpapers.ToList().FindAll(x => x.FolderPath == metaData.FolderPath);
            if (items.Count == 0)
            {
                try
                {
                    monitor = selectedMonitor;
                    var dataFolder = Path.Combine(_userSettings.Settings.WallpaperDir, Constants.CommonPaths.TempDir);
                    if (monitor?.Content != null)
                    {
                        string wpdataFolder = null;
                        switch (arrangement)
                        {
                            case WallpaperArrangement.Per:
                                wpdataFolder = Path.Combine(dataFolder, metaData.FolderPath, monitor.Content);
                                break;
                            case WallpaperArrangement.Expand:
                                wpdataFolder = Path.Combine(dataFolder, metaData.FolderPath, "Expand");
                                break;
                            case WallpaperArrangement.Duplicate:
                                wpdataFolder = Path.Combine(dataFolder, metaData.FolderPath, "Duplicate");
                                break;
                        }
                        Directory.CreateDirectory(wpdataFolder);
                        //copy the original file if not found..
                        wpCustomizePathUsing = Path.Combine(wpdataFolder, "WpCustomize.json");
                        if (!File.Exists(wpCustomizePathUsing))
                        {
                            File.Copy(metaData.WpCustomizePath, wpCustomizePathUsing);
                        }
                    }
                    else
                    {
                        //todo: fallback, use the original file (restore feature disabled.)
                    }
                }
                catch (Exception e)
                {
                    //todo: fallback, use the original file (restore feature disabled.)
                    _logger.Error(e.ToString());
                }
            }
            else if (items.Count == 1)
            {
                //send regardless of selected monitor, if wallpaper is running on non-selected monitor - its modified instead.
                wpCustomizePathUsing = items[0].WpCustomizePathUsing;
                var monitors = _monitorManager.Monitors.FirstOrDefault(x => x.Equals(items[0].Monitor));
            }
            else
            {
                switch (arrangement)
                {
                    case WallpaperArrangement.Per:
                        {
                            //more than one _monitor; if selected monitor, sendpath otherwise send the first one found.
                            int index = items.FindIndex(x => selectedMonitor.Equals(x.Monitor));
                            wpCustomizePathUsing = index != -1 ? items[index].WpCustomizePathUsing : items[0].WpCustomizePathUsing;
                            monitor = index != -1 ? items[index].Monitor : items[0].Monitor;
                        }
                        break;
                    case WallpaperArrangement.Expand:
                    case WallpaperArrangement.Duplicate:
                        {
                            wpCustomizePathUsing = items[0].WpCustomizePathUsing;
                            monitor = items[0].Monitor;
                        }
                        break;
                }
            }

            return (wpCustomizePathUsing, monitor);
        }

        //private void Slider_GotFocus(object sender, RoutedEventArgs e)
        //{
        //    var mainWindow = App.Services.GetRequiredService<MainWindow>();
        //    mainWindow.Changedtransparent(true);
        //}

        //private void Slider_LostFocus(object sender, RoutedEventArgs e)
        //{
        //    var mainWindow = App.Services.GetRequiredService<MainWindow>();
        //    mainWindow.Changedtransparent(false);
        //}
        #endregion

        //private void EyeDropBtn_Click(object sender, RoutedEventArgs e)
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

        internal async void SendMessage()
        {
            await _wpControl.SendMessageWallpaperAsync(_monitor, _metaData, new VirtualPaperInitFilterCmd());

            foreach (var item in _wpCustomizeData)
            {
                string uiElementType = item.Value["Type"].ToString();
                if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase))
                {
                    await _wpControl.SendMessageWallpaperAsync(_monitor, _metaData, new VirtualPaperSlider()
                    {
                        Name = item.Key,
                        Value = (double)item.Value["Value"]
                    });
                }
                else if (uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase))
                {
                    await _wpControl.SendMessageWallpaperAsync(_monitor, _metaData, new VirtualPaperTextBox()
                    {
                        Name = item.Key,
                        Value = item.Value["Value"].ToString(),
                    });
                }
                else if (uiElementType.Equals("CheckBox", StringComparison.OrdinalIgnoreCase))
                {
                    await _wpControl.SendMessageWallpaperAsync(_monitor, _metaData, new VirtualPaperCheckbox()
                    {
                        Name = item.Key,
                        Value = (bool)item.Value["Value"],
                    });
                }
                else if (uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase))
                {
                    await _wpControl.SendMessageWallpaperAsync(_monitor, _metaData, new VirtualPaperDropdown()
                    {
                        Name = item.Key,
                        Value =  (int)item.Value["Value"],
                    });
                }
            }

            await _wpControl.SendMessageWallpaperAsync(_monitor, _metaData, new VirtualPaperApplyCmd());
        }

        private void OnCustomizeValueChanged(IntValueChangedEventArgs e)
        {
            UpdatePropertyFile(false);
            _intValueChanged?.Invoke(this, e);
        }
        
        private void OnCustomizeValueChanged(DoubleValueChangedEventArgs e)
        {
            UpdatePropertyFile(false);
            _doubleValueChanged?.Invoke(this, e);
        }

        private void OnCustomizeValueChanged(BoolValueChangedEventArgs e)
        {
            UpdatePropertyFile(false);
            _boolValueChanged?.Invoke(this, e);
        }

        private void OnCustomizeValueChanged(StringValueChangedEventArgs e)
        {
            UpdatePropertyFile(false);
            _stringValueChanged?.Invoke(this, e);
        }

        #region dispose
        private bool _isDisposed;
        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _intValueChanged -= _observer.OnCustomizeValueChanged;
                    _doubleValueChanged -= _observer.OnCustomizeValueChanged;
                    _boolValueChanged -= _observer.OnCustomizeValueChanged;
                    _stringValueChanged -= _observer.OnCustomizeValueChanged;
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private string _wpCustomizePathUsing;
        private string _wpCustomizePathTmp;
        private IMetaData _metaData;
        private IMonitor _monitor;
        private JObject _wpCustomizeData;
        private readonly Thickness _margin = new(0, 0, 20, 10);
        private readonly double _minWidth = 200;
        private readonly ICustomizeValueChangedObserver _observer;
        private readonly IUserSettingsClient _userSettings;
        private readonly IWallpaperControlClient _wpControl;
        private readonly IMonitorManagerClient _monitorManager;
        //private readonly DispatcherQueue _dispatcherQueue;
        private readonly object _restoreLock = new();
        private EventHandler<IntValueChangedEventArgs> _intValueChanged;
        private EventHandler<DoubleValueChangedEventArgs> _doubleValueChanged;
        private EventHandler<BoolValueChangedEventArgs> _boolValueChanged;
        private EventHandler<StringValueChangedEventArgs> _stringValueChanged;
    }
}
