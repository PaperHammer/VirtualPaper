using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;
using Windows.System;
using WinUI3Localizer;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Data {
    public sealed partial class Edits : UserControl {      
        public bool IsSaved { get; private set; }
        public string Text_Edit_Title { get; set; }
        public string Text_Edit_Desc { get; set; }
        public string Text_Edit_Tags { get; set; }
        public string Text_SaveAndApply { get; set; }

        public string Title {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(Edits), new PropertyMetadata(string.Empty));

        public string Desc {
            get { return (string)GetValue(DescProperty); }
            set { SetValue(DescProperty, value); }
        }
        public static readonly DependencyProperty DescProperty =
            DependencyProperty.Register("Desc", typeof(string), typeof(Edits), new PropertyMetadata(string.Empty));

        public ObservableCollection<string> TagList { get; set; }

        public Edits() {
            this.InitializeComponent();
            _localizer = LanguageUtil.LocalizerInstacne;
            InitText();
        }

        public Edits(IWpBasicData wpBasicData, WindowEx windowEx) : this() {
            _windowEx = windowEx;
            _data = wpBasicData;
            Init();
        }

        private void InitText() {
            Text_Edit_Title = _localizer.GetLocalizedString(Constants.I18n.Text_Edit_Title);
            Text_Edit_Desc = _localizer.GetLocalizedString(Constants.I18n.Text_Edit_Desc);
            Text_Edit_Tags = _localizer.GetLocalizedString(Constants.I18n.Text_Edit_Tags);
            Text_SaveAndApply = _localizer.GetLocalizedString(Constants.I18n.Text_SaveAndApply);
        }

        private void Init() {
            Title = _data.Title;
            Desc = _data.Desc;
            TagList = [.. _data.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries)];
        }

        private void TagInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) {
            if (e.Key == VirtualKey.Enter) {
                string tagText = tagInput.Text.TrimStart().TrimEnd();
                if (TagList.Contains(tagText)) {
                    return;                    
                }
                tagInput.Text = string.Empty;
                TagList.Add(tagText);
            }
        }

        private void TagDelButton_Click(object sender, RoutedEventArgs e) {
            var button = sender as Button;

            if (button != null) {
                var stackPanel = button.Parent as StackPanel;

                if (stackPanel != null) {
                    foreach (var child in stackPanel.Children) {
                        var textBlock = child as TextBlock;
                        if (textBlock != null) {
                            string tagText = textBlock.Text;
                            TagList.Remove(tagText);
                            break;
                        }
                    }
                }
            }            
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) {
            IsSaved = true;
            _windowEx?.Close();
        }

        private readonly ILocalizer _localizer;
        private readonly WindowEx _windowEx;
        private readonly IWpBasicData _data;
    }
}

