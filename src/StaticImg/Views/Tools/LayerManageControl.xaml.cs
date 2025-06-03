using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Views.Components;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class LayerManageControl : UserControl {
        public event EventHandler AddLayerRequest;
        public event EventHandler<long> CopyLayerRequest;
        public event EventHandler<long> RenameLayerRequest;
        public event EventHandler<long> DeleteLayerRequest;

        public InkCanvasData SelectedInkCanvas {
            get { return (InkCanvasData)GetValue(SelectedInkCanvasProperty); }
            set { SetValue(SelectedInkCanvasProperty, value); }
        }
        public static readonly DependencyProperty SelectedInkCanvasProperty =
            DependencyProperty.Register("SelectedInkCanvas", typeof(InkCanvasData), typeof(LayerManageControl), new PropertyMetadata(null));

        public ObservableList<InkCanvasData> InkDatas {
            get { return (ObservableList<InkCanvasData>)GetValue(InkDatasProperty); }
            set { SetValue(InkDatasProperty, value); }
        }
        public static readonly DependencyProperty InkDatasProperty =
            DependencyProperty.Register("InkDatas", typeof(ObservableList<InkCanvasData>), typeof(LayerManageControl), new PropertyMetadata(null));

        public LayerManageControl() {
            this.InitializeComponent();
        }

        private void AddLayer_Click(object sender, RoutedEventArgs e) {
            //await inkCanvas.AddLayerAsync();
            AddLayerRequest?.Invoke(this, EventArgs.Empty);
        }

        private void CopyLayer_Click(object sender, RoutedEventArgs e) {
            //await inkCanvas.CopyLayerAsync(_rightTappedItem.ItemTag);
            CopyLayerRequest?.Invoke(this, _rightTappedItem.ItemTag);
        }

        private void RenameLayer_Click(object sender, RoutedEventArgs e) {
            //await inkCanvas.RenameAsync(_rightTappedItem.ItemTag);
            RenameLayerRequest?.Invoke(this, _rightTappedItem.ItemTag);
        }

        private void DeleteLayer_Click(object sender, RoutedEventArgs e) {
            //await inkCanvas.DeleteAsync(_rightTappedItem.ItemTag);
            DeleteLayerRequest?.Invoke(this, _rightTappedItem.ItemTag);
        }

        private void Listview_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            var container = layersListView.ContainerFromItem((e.OriginalSource as FrameworkElement).DataContext) as ListViewItem;
            _rightTappedItem = container.Content as LayerItem;
        }

        private LayerItem _rightTappedItem;

        private readonly string _SIG_Text_AddLayer = nameof(Constants.I18n.SIG_Text_AddLayer); // ÐÂÔöÍ¼²ã
        private readonly string _SIG_Text_CopyLayer = nameof(Constants.I18n.SIG_Text_CopyLayer); // ¸´ÖÆÍ¼²ã
        private readonly string _SIG_Text_RenameLayer = nameof(Constants.I18n.SIG_Text_RenameLayer); // É¾³ýÍ¼²ã
        private readonly string _SIG_Text_DeleteLayer = nameof(Constants.I18n.SIG_Text_DeleteLayer); // ÖØÃüÃûÍ¼²ã
    }
}
