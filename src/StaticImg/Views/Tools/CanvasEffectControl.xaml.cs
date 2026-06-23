using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Collection;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class CanvasEffectControl : UserControl {
        public event EventHandler<RoutedEventArgs>? CanvasEffectCancel;
        public event EventHandler<RoutedEventArgs>? CanvasEffectCommit;
        public event EventHandler<string>? EffectPreviewRequested;

        // 当前选中的效果 ID
        public string? ClickedEffectId {
            get => (string?)GetValue(ClickedEffectIdProperty);
            set => SetValue(ClickedEffectIdProperty, value);
        }
        public static readonly DependencyProperty ClickedEffectIdProperty =
            DependencyProperty.Register(
                nameof(ClickedEffectId),
                typeof(string),
                typeof(CanvasEffectControl),
                new PropertyMetadata(null));

        // 内容区展开目标高度：文本列表模式
        private const double ExpandedMaxHeight = 260.0;
        private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(180));

        public CanvasEffectControl() {
            InitializeComponent();
            InitEffectGroups();
            // 清空初始选中（ArcListView 的 TrySelectFirstItem 会自动选中首项）
            ClearAllSelections();
        }

        // 初始化各分组数据
        private void InitEffectGroups() {
            // 调整 (10)
            EffectGrid_Adjust.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "adjust_grayscale",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_GrayScale)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_GrayScale)) },
                new() { EffectId = "adjust_invert",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Invert)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Invert)) },
                new() { EffectId = "adjust_exposure",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Exposure)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Exposure)) },
                new() { EffectId = "adjust_brightness",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Brightness)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Brightness)) },
                new() { EffectId = "adjust_saturation",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Saturation)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Saturation)) },
                new() { EffectId = "adjust_hue",         EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Hue)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Hue)) },
                new() { EffectId = "adjust_contrast",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Contrast)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Contrast)) },
                new() { EffectId = "adjust_temperature", EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Temperature)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Temperature)) },
                new() { EffectId = "adjust_highlights",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Highlights)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Highlights)) },
                new() { EffectId = "color_sepia",        EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Sepia)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Sepia)) },
            };


            // 特效 (8)
            EffectGrid_Special.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "fx_blur",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Blur)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Blur)) },
                new() { EffectId = "fx_sharpen",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Sharpen)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Sharpen)) },
                new() { EffectId = "fx_noise",     EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Noise)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Noise)) },
                new() { EffectId = "fx_vignette",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Vignette)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Vignette)) },
                new() { EffectId = "fx_glow",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Glow)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Glow)) },
                new() { EffectId = "fx_bloom",     EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Bloom)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Bloom)) },
                new() { EffectId = "fx_distort",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Distort)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Distort)) },
            };

            // 混合 (4)
            EffectGrid_Blend.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "blend_multiply",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Multiply)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Multiply)) },
                new() { EffectId = "blend_screen",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Screen)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Screen)) },
                new() { EffectId = "blend_overlay",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Overlay)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_Overlay)) },
                new() { EffectId = "blend_softlight", EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_SoftLight)), EffectDescription = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_EffectDesc_SoftLight)) },
            };
        }

        // Accordion：打开当前组，折叠其他组
        private void GroupHeader_Checked(object sender, RoutedEventArgs e) {
            var clickedHeader = (ToggleButton)sender;
            var clickedContent = GetContentForHeader(clickedHeader);

            foreach (var (header, content) in AllGroups) {
                if (!ReferenceEquals(header, clickedHeader) && header.IsChecked == true) {
                    // 先取消选中（避免循环触发），再手动收起
                    header.IsChecked = false;
                    AnimateMaxHeight(content, to: 0);
                }
            }

            AnimateMaxHeight(clickedContent, to: ExpandedMaxHeight);
        }

        private void GroupHeader_Unchecked(object sender, RoutedEventArgs e) {
            var content = GetContentForHeader((ToggleButton)sender);
            AnimateMaxHeight(content, to: 0);
        }

        // MaxHeight 动画
        private static void AnimateMaxHeight(Grid target, double to) {
            var anim = new DoubleAnimation {
                To = to,
                Duration = AnimDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                EnableDependentAnimation = true,
            };
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, "MaxHeight");

            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Begin();
        }

        private void EffectGridView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is CanvasEffectItem item) {
                ClickedEffectId = item.EffectId;

                var clickedGrid = (ArcListView)sender;

                // 跨组互斥：取消其他所有 ArcListView 的选中
                foreach (var grid in AllGrids) {
                    if (!ReferenceEquals(grid, clickedGrid)) {
                        grid.ClearSelection();
                    }
                }

                EffectPreviewRequested?.Invoke(this, item.EffectId);
            }
        }

        internal void ClearAllSelections() {
            foreach (var grid in AllGrids) {
                grid.ClearSelection();
            }
        }

        // Header to Content 映射
        private Grid GetContentForHeader(ToggleButton header) {
            if (ReferenceEquals(header, Header_Adjust)) return Content_Adjust;
            if (ReferenceEquals(header, Header_Special)) return Content_Special;
            return Content_Blend;
        }

        private IEnumerable<(ToggleButton header, Grid content)> AllGroups =>
        [
            (Header_Adjust,   Content_Adjust),
            (Header_Special,  Content_Special),
            (Header_Blend,    Content_Blend),
        ];

        private IEnumerable<ArcListView> AllGrids =>
        [
            EffectGrid_Adjust,
            EffectGrid_Special,
            EffectGrid_Blend,
        ];

        // 底部按钮
        private void SelectCancelBtn_Click(object sender, RoutedEventArgs e) {
            Restore();
            CanvasEffectCancel?.Invoke(this, e);
        }

        private void SelectCommitBtn_Click(object sender, RoutedEventArgs e) {
            ClearAllSelections();
            CanvasEffectCommit?.Invoke(this, e);
        }

        internal void Restore() {
            ClickedEffectId = null;
            ClearAllSelections();

            foreach (var (header, content) in AllGroups) {
                if (header.IsChecked == true) {
                    header.IsChecked = false;
                    AnimateMaxHeight(content, to: 0);
                }
            }
        }        
    }
}
