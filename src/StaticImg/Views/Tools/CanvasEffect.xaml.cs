using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using Workloads.Creation.StaticImg.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class CanvasEffect : UserControl {

        // ── 公开事件 ─────────────────────────────────────────────────
        public event EventHandler<RoutedEventArgs>? CanvasEffectCancel;
        public event EventHandler<RoutedEventArgs>? CanvasEffectCommit;

        // ── 当前选中的效果 ID ────────────────────────────────────────
        public string? ClickedEffectId {
            get => (string?)GetValue(ClickedEffectIdProperty);
            set => SetValue(ClickedEffectIdProperty, value);
        }
        public static readonly DependencyProperty ClickedEffectIdProperty =
            DependencyProperty.Register(
                nameof(ClickedEffectId),
                typeof(string),
                typeof(CanvasEffect),
                new PropertyMetadata(null));

        // 内容区展开目标高度：3 列 × 3 行缩图（行高约 88px）+ 边距
        private const double ExpandedMaxHeight = 290.0;
        private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(180));

        // ── 构造 ─────────────────────────────────────────────────────
        public CanvasEffect() {
            InitializeComponent();
            InitEffectGroups();
        }

        // ── 初始化各分组数据 ─────────────────────────────────────────
        private void InitEffectGroups() {
            // 调整 (9)
            EffectGrid_Adjust.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "adjust_grayscale",   NameKey = "灰度",       PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_invert",      NameKey = "反转",       PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_exposure",    NameKey = "曝光",       PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_brightness",  NameKey = "亮度",       PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_saturation",  NameKey = "饱和",       PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_hue",         NameKey = "色相旋转",   PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_contrast",    NameKey = "对比度",     PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_temperature", NameKey = "冷暖",       PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_highlights",  NameKey = "高光和阴影", PreviewImagePath = string.Empty },
            };

            // 颜色 (4)
            EffectGrid_Color.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "color_sepia",   NameKey = "褪色",   PreviewImagePath = string.Empty },
                new() { EffectId = "color_duotone", NameKey = "双色调", PreviewImagePath = string.Empty },
                new() { EffectId = "color_lut",     NameKey = "LUT",    PreviewImagePath = string.Empty },
                new() { EffectId = "color_tint",    NameKey = "着色",   PreviewImagePath = string.Empty },
            };

            // 艺术 (8)
            EffectGrid_Artistic.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "art_oilpaint",    NameKey = "油画",   PreviewImagePath = string.Empty },
                new() { EffectId = "art_sketch",      NameKey = "素描",   PreviewImagePath = string.Empty },
                new() { EffectId = "art_watercolor",  NameKey = "水彩",   PreviewImagePath = string.Empty },
                new() { EffectId = "art_pixelate",    NameKey = "像素化", PreviewImagePath = string.Empty },
                new() { EffectId = "art_emboss",      NameKey = "浮雕",   PreviewImagePath = string.Empty },
                new() { EffectId = "art_pointillism", NameKey = "点彩",   PreviewImagePath = string.Empty },
                new() { EffectId = "art_crosshatch",  NameKey = "交叉线", PreviewImagePath = string.Empty },
                new() { EffectId = "art_cartoon",     NameKey = "卡通",   PreviewImagePath = string.Empty },
            };

            // 特效 (8)
            EffectGrid_Special.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "fx_blur",      NameKey = "模糊", PreviewImagePath = string.Empty },
                new() { EffectId = "fx_sharpen",   NameKey = "锐化", PreviewImagePath = string.Empty },
                new() { EffectId = "fx_noise",     NameKey = "噪点", PreviewImagePath = string.Empty },
                new() { EffectId = "fx_vignette",  NameKey = "暗角", PreviewImagePath = string.Empty },
                new() { EffectId = "fx_glow",      NameKey = "发光", PreviewImagePath = string.Empty },
                new() { EffectId = "fx_bloom",     NameKey = "光晕", PreviewImagePath = string.Empty },
                new() { EffectId = "fx_chromatic", NameKey = "色差", PreviewImagePath = string.Empty },
                new() { EffectId = "fx_distort",   NameKey = "扭曲", PreviewImagePath = string.Empty },
            };

            // 混合 (4)
            EffectGrid_Blend.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "blend_multiply",  NameKey = "正片叠底", PreviewImagePath = string.Empty },
                new() { EffectId = "blend_screen",    NameKey = "滤色",     PreviewImagePath = string.Empty },
                new() { EffectId = "blend_overlay",   NameKey = "叠加",     PreviewImagePath = string.Empty },
                new() { EffectId = "blend_softlight", NameKey = "柔光",     PreviewImagePath = string.Empty },
            };
        }

        // ── Accordion：打开当前组，折叠其他组 ───────────────────────
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

        // ── MaxHeight 动画 ────────────────────────────────────────────
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

        // ── 效果缩略图点击 ───────────────────────────────────────────
        private void EffectGridView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is CanvasEffectItem item) {
                ClickedEffectId = item.EffectId;

                // 清除其他组的视觉选中态
                foreach (var grid in AllEffectGridViews) {
                    if (!ReferenceEquals(grid, sender)) {
                        grid.SelectedItem = null;
                    }
                }
            }
        }

        // ── Header ↔ Content 映射 ────────────────────────────────────
        private Grid GetContentForHeader(ToggleButton header) {
            if (ReferenceEquals(header, Header_Adjust)) return Content_Adjust;
            if (ReferenceEquals(header, Header_Color)) return Content_Color;
            if (ReferenceEquals(header, Header_Artistic)) return Content_Artistic;
            if (ReferenceEquals(header, Header_Special)) return Content_Special;
            return Content_Blend;
        }

        private IEnumerable<(ToggleButton header, Grid content)> AllGroups =>
        [
            (Header_Adjust,   Content_Adjust),
            (Header_Color,    Content_Color),
            (Header_Artistic, Content_Artistic),
            (Header_Special,  Content_Special),
            (Header_Blend,    Content_Blend),
        ];

        private IEnumerable<GridView> AllEffectGridViews =>
        [
            EffectGrid_Adjust,
            EffectGrid_Color,
            EffectGrid_Artistic,
            EffectGrid_Special,
            EffectGrid_Blend,
        ];

        // ── 底部按钮 ─────────────────────────────────────────────────
        private void SelectCancelBtn_Click(object sender, RoutedEventArgs e) {
            ClickedEffectId = null;

            foreach (var (header, content) in AllGroups) {
                if (header.IsChecked == true) {
                    header.IsChecked = false;
                    AnimateMaxHeight(content, to: 0);
                }
            }
            foreach (var grid in AllEffectGridViews) {
                grid.SelectedItem = null;
            }

            CanvasEffectCancel?.Invoke(this, e);
        }

        private void SelectCommitBtn_Click(object sender, RoutedEventArgs e) {
            CanvasEffectCommit?.Invoke(this, e);
        }
    }
}
