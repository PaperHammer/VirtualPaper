using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Models.Cores;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebProjectInfoControl : UserControl {
        public event EventHandler<WpWebProjectData>? ProjectInfoSaved;

        private WpWebProjectData _data = new();

        public WebProjectInfoControl() {
            InitializeComponent();
        }

        public void Load(WpWebProjectData data) {
            _data = data;
            TitleBox.Text = data.Title;
            DescBox.Text = data.Desc;
            AuthorBox.Text = data.Authors;
            TagsBox.Text = data.Tags;
            EntryFileBox.Text = data.File;
        }

        private void TitleBox_TextChanged(object sender, TextChangedEventArgs e) => _data.Title = TitleBox.Text;
        private void DescBox_TextChanged(object sender, TextChangedEventArgs e) => _data.Desc = DescBox.Text;
        private void AuthorBox_TextChanged(object sender, TextChangedEventArgs e) => _data.Authors = AuthorBox.Text;
        private void TagsBox_TextChanged(object sender, TextChangedEventArgs e) => _data.Tags = TagsBox.Text;
        private void EntryFileBox_TextChanged(object sender, TextChangedEventArgs e) => _data.File = EntryFileBox.Text;

        private void SaveProjectInfo_Click(object sender, RoutedEventArgs e) {
            ProjectInfoSaved?.Invoke(this, _data);
        }
    }
}
