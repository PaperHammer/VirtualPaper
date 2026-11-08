using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using Windows.System;

namespace VirtualPaper.WpSettingsPanel.Views {
    public sealed partial class WallpaperDetailsEditor : UserControl {
        public WallpaperDetailsEditor(IWpBasicData data, WallpaperOrganization organization) {
            _authors = new([data.Authors], splitCommas: false);
            _tags = new(organization.Tags, splitCommas: true);
            InitializeComponent();
            TitleInput.Text = data.Title;
            DescriptionInput.Text = data.Desc;
            SourceTagsSummary.Text = string.Format(LanguageUtil.GetI18n("WpLib_IncludedTags"),
                string.Join(" · ", WallpaperOrganization.NormalizeTags([data.Tags])));
            SourceTagsSummary.Visibility = string.IsNullOrWhiteSpace(data.Tags) ? Visibility.Collapsed : Visibility.Visible;
        }

        internal WallpaperDetailsEdit ReadEdit() => new(TitleInput.Text, DescriptionInput.Text,
            _authors.Serialize(AuthorInput.Text), _tags.Serialize(PersonalTagsInput.Text));

        private void TokenInput_KeyDown(object sender, KeyRoutedEventArgs args) {
            if (args.Key != VirtualKey.Enter || sender is not TextBox input) return;
            // Consume Enter so it adds an item instead of invoking the dialog's Save.
            args.Handled = true;
            (input == AuthorInput ? _authors : _tags).Add(input.Text);
            input.Text = string.Empty;
        }

        private void RemoveToken_Click(object sender, RoutedEventArgs args) {
            if (sender is not Button { Tag: string item } button) return;
            DependencyObject? parent = button;
            while (parent != null && parent is not ItemsRepeater) parent = VisualTreeHelper.GetParent(parent);
            if (parent == AuthorsList) _authors.Items.Remove(item);
            else if (parent == TagsList) _tags.Items.Remove(item);
        }

        private readonly WallpaperInfoTokens _authors;
        private readonly WallpaperInfoTokens _tags;
    }
}
