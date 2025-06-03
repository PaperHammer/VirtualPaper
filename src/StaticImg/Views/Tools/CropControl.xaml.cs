using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Workloads.Creation.StaticImg.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class CropControl : UserControl {
        public event EventHandler<RoutedEventArgs> CropCancelRequest;
        public event EventHandler<RoutedEventArgs> CropCommitRequest;

        public AspectRatioItem SeletcedAspect {
            get { return (AspectRatioItem)GetValue(SeletcedAspectProperty); }
            set { SetValue(SeletcedAspectProperty, value); }
        }
        public static readonly DependencyProperty SeletcedAspectProperty =
            DependencyProperty.Register(nameof(SeletcedAspect), typeof(AspectRatioItem), typeof(CropControl), new PropertyMetadata(null));

        public List<AspectRatioItem> AspectRatios {
            get { return (List<AspectRatioItem>)GetValue(AspectRatiosProperty); }
            set { SetValue(AspectRatiosProperty, value); }
        }
        public static readonly DependencyProperty AspectRatiosProperty =
            DependencyProperty.Register(nameof(AspectRatios), typeof(List<AspectRatioItem>), typeof(CropControl), new PropertyMetadata(null));

        public CropControl() {
            this.InitializeComponent();
        }

        private void AspectRatio_ItemClick(object sender, ItemClickEventArgs e) {
            //inkCanvas._viewModel.ConfigData.SeletcedAspectitem = e.ClickedItem as AspectRatioItem;
        }

        private void CropCancelBtn_Click(object sender, RoutedEventArgs e) {
            //inkCanvas.CancelCrop();
            CropCancelRequest?.Invoke(this, e);
        }

        private void CropCommitBtn_Click(object sender, RoutedEventArgs e) {
            //inkCanvas.CommitCrop();
            CropCommitRequest?.Invoke(this, e);
        }
    }

    public enum CropRequest {
        Commit,
        Cancel
    }
}
