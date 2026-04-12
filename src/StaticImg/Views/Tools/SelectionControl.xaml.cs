using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class SelectionControl : UserControl {
        public event EventHandler<RoutedEventArgs> SelectCancel;
        public event EventHandler<RoutedEventArgs> SelectCommit;

        public SelectionControl() {
            this.InitializeComponent();
        }

        private void SelectCancelBtn_Click(object sender, RoutedEventArgs e) {
            SelectCancel?.Invoke(this, e);
        }

        private void SelectCommitBtn_Click(object sender, RoutedEventArgs e) {
            SelectCommit?.Invoke(this, e);
        }
    }

    public enum SeletionRequest {
        Commit,
        Cancel
    }
}
