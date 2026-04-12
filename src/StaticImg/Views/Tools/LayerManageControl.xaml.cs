using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Collection;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Views.Components;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class LayerManageControl : UserControl {
        public event EventHandler<ItemMoveEventArgs> MoveLayerRequest;
        public event EventHandler<Guid> AddLayerRequest;
        public event EventHandler<Guid> CopyLayerRequest;
        public event EventHandler<Guid> RenameLayerRequest;
        public event EventHandler<Guid> DeleteLayerRequest;

        public LayerInfo SelectedLayer {
            get { return (LayerInfo)GetValue(SelectedLayerProperty); }
            set { SetValue(SelectedLayerProperty, value); }
        }
        public static readonly DependencyProperty SelectedLayerProperty =
            DependencyProperty.Register(nameof(SelectedLayer), typeof(LayerInfo), typeof(LayerManageControl), new PropertyMetadata(null));

        public ObservableCollection<LayerInfo> Layers {
            get { return (ObservableCollection<LayerInfo>)GetValue(LayersProperty); }
            set { SetValue(LayersProperty, value); }
        }
        public static readonly DependencyProperty LayersProperty =
            DependencyProperty.Register(nameof(Layers), typeof(ObservableCollection<LayerInfo>), typeof(LayerManageControl), new PropertyMetadata(null));

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
            AddLayerRequest?.Invoke(sender, _rightTappedItem.ItemTag);
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

        private void LayersListView_ItemsMoved(object sender, ItemMoveEventArgs e) {
            MoveLayerRequest?.Invoke(sender, e);
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
        private readonly string _SIG_Text_AddLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_AddLayer)); // 新增图层
        private readonly string _SIG_Text_CopyLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_CopyLayer)); // 复制图层
        private readonly string _SIG_Text_RenameLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_RenameLayer)); // 删除图层
        private readonly string _SIG_Text_DeleteLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_DeleteLayer)); // 重命名图层
    }
}
