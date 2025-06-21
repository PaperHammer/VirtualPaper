using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Views.Components;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class LayerManageControl : UserControl {
        public event EventHandler MoveLayerRequest;
        public event EventHandler AddLayerRequest;
        public event EventHandler<long> CopyLayerRequest;
        public event EventHandler<long> RenameLayerRequest;
        public event EventHandler<long> DeleteLayerRequest;

        public InkCanvasData SelectedInkCanvas {
            get { return (InkCanvasData)GetValue(SelectedInkCanvasProperty); }
            set { SetValue(SelectedInkCanvasProperty, value); }
        }
        public static readonly DependencyProperty SelectedInkCanvasProperty =
            DependencyProperty.Register(nameof(SelectedInkCanvas), typeof(InkCanvasData), typeof(LayerManageControl), new PropertyMetadata(null));

        public ObservableList<InkCanvasData> InkDatas {
            get { return (ObservableList<InkCanvasData>)GetValue(InkDatasProperty); }
            set { SetValue(InkDatasProperty, value); }
        }
        public static readonly DependencyProperty InkDatasProperty =
            DependencyProperty.Register(nameof(InkDatas), typeof(ObservableList<InkCanvasData>), typeof(LayerManageControl), new PropertyMetadata(null));

        public bool IsAllwaysSeletedNewItem {
            get { return (bool)GetValue(IsAllwaysSeletedNewItemProperty); }
            set { SetValue(IsAllwaysSeletedNewItemProperty, value); }
        }
        public static readonly DependencyProperty IsAllwaysSeletedNewItemProperty =
            DependencyProperty.Register(nameof(IsAllwaysSeletedNewItem), typeof(bool), typeof(LayerManageControl), new PropertyMetadata(false));

        public LayerManageControl() {
            this.InitializeComponent();
        }

        private void AddLayer_Click(object sender, RoutedEventArgs e) {
            AddLayerRequest?.Invoke(sender, EventArgs.Empty);
        }

        private void CopyLayer_Click(object sender, RoutedEventArgs e) {
            CopyLayerRequest?.Invoke(sender, _rightTappedItem.ItemTag);
        }

        private void RenameLayer_Click(object sender, RoutedEventArgs e) {
            RenameLayerRequest?.Invoke(sender, _rightTappedItem.ItemTag);
        }

        private void DeleteLayer_Click(object sender, RoutedEventArgs e) {
            DeleteLayerRequest?.Invoke(sender, _rightTappedItem.ItemTag);
        }

        private void LayersListView_ItemsMoved(object sender, EventArgs e) {
            MoveLayerRequest?.Invoke(sender, EventArgs.Empty);
        }

        private void LayersListView_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            var container = LayersListView.ContainerFromItem((e.OriginalSource as FrameworkElement)?.DataContext) as ListViewItem;
            _rightTappedItem = container.Content as LayerItem;
            RightClick();
        }

        private void RightClick() {
            if (_rightTappedItem == null) {
                LayersListView.ContextFlyout.Hide();
            }
            else {
                LayersListView.ContextFlyout.ShowAt(_rightTappedItem);
            }
        }

        private LayerItem _rightTappedItem;
        private readonly string _SIG_Text_AddLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_AddLayer)); // ÐÂÔöÍ¼²ã
        private readonly string _SIG_Text_CopyLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_CopyLayer)); // ¸´ÖÆÍ¼²ã
        private readonly string _SIG_Text_RenameLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_RenameLayer)); // É¾³ýÍ¼²ã
        private readonly string _SIG_Text_DeleteLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_DeleteLayer)); // ÖØÃüÃûÍ¼²ã
    }
}
