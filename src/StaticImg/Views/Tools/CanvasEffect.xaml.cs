using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class CanvasEffect : UserControl {

        // ── 公开事件 ─────────────────────────────────────────────────
        public event EventHandler<RoutedEventArgs>? CanvasEffectCancel;
        public event EventHandler<RoutedEventArgs>? CanvasEffectCommit;
        public event EventHandler<string>? EffectPreviewRequested;

        /// <summary>是否应用到全部图层</summary>
        public bool ApplyAllLayers => ApplyAllLayersToggle.IsOn;

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
                new() { EffectId = "adjust_grayscale",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_GrayScale)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_invert",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Invert)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_exposure",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Exposure)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_brightness",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Brightness)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_saturation",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Saturation)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_hue",         EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Hue)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_contrast",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Contrast)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_temperature", EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Temperature)), PreviewImagePath = string.Empty },
                new() { EffectId = "adjust_highlights",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Highlights)), PreviewImagePath = string.Empty },
            };

            // 颜色 (4)
            EffectGrid_Color.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "color_sepia",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Sepia)), PreviewImagePath = string.Empty },
                new() { EffectId = "color_duotone", EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Duotone)), PreviewImagePath = string.Empty },
                new() { EffectId = "color_lut",     EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_LUT)), PreviewImagePath = string.Empty },
                new() { EffectId = "color_tint",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Tint)), PreviewImagePath = string.Empty },
            };

            // 艺术 (8)
            EffectGrid_Artistic.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "art_oilpaint",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_OilPaint)), PreviewImagePath = string.Empty },
                new() { EffectId = "art_sketch",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Sketch)), PreviewImagePath = string.Empty },
                new() { EffectId = "art_watercolor",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_WaterColor)), PreviewImagePath = string.Empty },
                new() { EffectId = "art_pixelate",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Pixelate)), PreviewImagePath = string.Empty },
                new() { EffectId = "art_emboss",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Emboss)), PreviewImagePath = string.Empty },
                new() { EffectId = "art_pointillism", EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Pointillism)), PreviewImagePath = string.Empty },
                new() { EffectId = "art_crosshatch",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Crosshatch)), PreviewImagePath = string.Empty },
                new() { EffectId = "art_cartoon",     EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Cartoon)), PreviewImagePath = string.Empty },
            };

            // 特效 (8)
            EffectGrid_Special.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "fx_blur",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Blur)), PreviewImagePath = string.Empty },
                new() { EffectId = "fx_sharpen",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Sharpen)), PreviewImagePath = string.Empty },
                new() { EffectId = "fx_noise",     EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Noise)), PreviewImagePath = string.Empty },
                new() { EffectId = "fx_vignette",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Vignette)), PreviewImagePath = string.Empty },
                new() { EffectId = "fx_glow",      EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Glow)), PreviewImagePath = string.Empty },
                new() { EffectId = "fx_bloom",     EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Bloom)), PreviewImagePath = string.Empty },
                new() { EffectId = "fx_chromatic", EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Chromatic)), PreviewImagePath = string.Empty },
                new() { EffectId = "fx_distort",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Distort)), PreviewImagePath = string.Empty },
            };

            // 混合 (4)
            EffectGrid_Blend.ItemsSource = new List<CanvasEffectItem> {
                new() { EffectId = "blend_multiply",  EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Multiply)), PreviewImagePath = string.Empty },
                new() { EffectId = "blend_screen",    EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Screen)), PreviewImagePath = string.Empty },
                new() { EffectId = "blend_overlay",   EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_Overlay)), PreviewImagePath = string.Empty },
                new() { EffectId = "blend_softlight", EffectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_Effect_SoftLight)), PreviewImagePath = string.Empty },
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
                EffectPreviewRequested?.Invoke(this, item.EffectId);
            }
        }

        // Header to Content 映射
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

        // 底部按钮
        private void SelectCancelBtn_Click(object sender, RoutedEventArgs e) {
            ClickedEffectId = null;

            foreach (var (header, content) in AllGroups) {
                if (header.IsChecked == true) {
                    header.IsChecked = false;
                    AnimateMaxHeight(content, to: 0);
                }
            }

            CanvasEffectCancel?.Invoke(this, e);
        }

        private void SelectCommitBtn_Click(object sender, RoutedEventArgs e) {
            CanvasEffectCommit?.Invoke(this, e);
        }
    }
}
