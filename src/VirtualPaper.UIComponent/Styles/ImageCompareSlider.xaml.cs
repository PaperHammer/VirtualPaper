using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace VirtualPaper.UIComponent.Styles {
    public sealed partial class ImageCompareSlider : UserControl {
        // 把手宽度，DividerPanel.Width 要与之一致
        private const float DividerPanelWidth = 28f;

        #region Dependency Properties

        public string BeforeImageSourceKey {
            get => (string)GetValue(BeforeImageSourceKeyProperty);
            set => SetValue(BeforeImageSourceKeyProperty, value);
        }
        public static readonly DependencyProperty BeforeImageSourceKeyProperty =
            DependencyProperty.Register(nameof(BeforeImageSourceKey), typeof(string),
                typeof(ImageCompareSlider), new PropertyMetadata(null));

        public string AfterImageSourceKey {
            get => (string)GetValue(AfterImageSourceKeyProperty);
            set => SetValue(AfterImageSourceKeyProperty, value);
        }
        public static readonly DependencyProperty AfterImageSourceKeyProperty =
            DependencyProperty.Register(nameof(AfterImageSourceKey), typeof(string),
                typeof(ImageCompareSlider), new PropertyMetadata(null));

        public bool AutoAnimate {
            get => (bool)GetValue(AutoAnimateProperty);
            set => SetValue(AutoAnimateProperty, value);
        }
        public static readonly DependencyProperty AutoAnimateProperty =
            DependencyProperty.Register(nameof(AutoAnimate), typeof(bool),
                typeof(ImageCompareSlider),
                new PropertyMetadata(true, OnAutoAnimateChanged));

        public TimeSpan AnimationDuration {
            get => (TimeSpan)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }
        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(nameof(AnimationDuration), typeof(TimeSpan),
                typeof(ImageCompareSlider),
                new PropertyMetadata(TimeSpan.FromSeconds(4)));

        public TimeSpan PauseDuration {
            get => (TimeSpan)GetValue(PauseDurationProperty);
            set => SetValue(PauseDurationProperty, value);
        }
        public static readonly DependencyProperty PauseDurationProperty =
            DependencyProperty.Register(nameof(PauseDuration), typeof(TimeSpan),
                typeof(ImageCompareSlider),
                new PropertyMetadata(TimeSpan.FromSeconds(0.8)));

        #endregion        

        public ImageCompareSlider() {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #region Lifecycle

        private void OnLoaded(object sender, RoutedEventArgs e) {
            EnsureComposition();
            ApplyStaticPosition(_currentRatio);

            if (AutoAnimate && !_isDragging) {
                if (_isFirstLoad) {
                    _isFirstLoad = false;
                    StartCompositionAnimation(_currentRatio, true, PauseDuration);
                }
                else {
                    StartCompositionAnimation(_currentRatio, _lastDirectionIsRight);
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            StopCompositionAnimation();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            _width = (float)e.NewSize.Width;
            _height = (float)e.NewSize.Height;

            if (_afterClip != null) {
                if (_animRunning) {
                    StopCompositionAnimation();
                    StartCompositionAnimation(_currentRatio, _lastDirectionIsRight);
                }
                else {
                    ApplyStaticPosition(_currentRatio);
                }
            }
        }

        #endregion

        #region Composition Setup

        private void EnsureComposition() {
            if (_compositor != null) return;

            _afterVisual = ElementCompositionPreview.GetElementVisual(AfterImage);
            _dividerVisual = ElementCompositionPreview.GetElementVisual(DividerPanel);
            _compositor = _afterVisual.Compositor;

            _afterClip = _compositor.CreateInsetClip(0, 0, _width, 0);
            _afterVisual.Clip = _afterClip;
        }

        #endregion

        #region Static Position (手动模式)

        private void ApplyStaticPosition(double ratio) {
            if (_afterClip == null || _width <= 0) return;

            ratio = Math.Clamp(ratio, 0, 1);
            float clipX = (float)(ratio * _width);

            _afterClip.LeftInset = 0;
            _afterClip.TopInset = 0;
            _afterClip.BottomInset = 0;
            _afterClip.RightInset = _width - clipX;

            if (_dividerVisual != null) {
                _dividerVisual.Offset = new Vector3(
                    clipX - DividerPanelWidth / 2f, 0, 0);
            }
        }

        #endregion

        #region Composition Animation (自动 - 匀速)

        /// <summary>
        /// 从指定 ratio 位置开始动画，并指定初始方向。
        /// </summary>
        /// <param name="startRatio">当前位置比例 0~1</param>
        /// <param name="goRightFirst">true = 先向右移动，false = 先向左移动</param>
        /// <param name="initialDelay">动画启动前的延迟时间（仅首次生效）</param>
        private void StartCompositionAnimation(double startRatio = 0.0, bool goRightFirst = true, TimeSpan? initialDelay = null) {
            if (_compositor == null || _width <= 0) return;
            StopCompositionAnimation();

            startRatio = Math.Clamp(startRatio, 0, 1);

            var linear = _compositor.CreateLinearEasingFunction();
            float halfPanel = DividerPanelWidth / 2f;

            float sweepSec = (float)AnimationDuration.TotalSeconds;
            float pauseSec = (float)PauseDuration.TotalSeconds;

            float startRightInset = (float)(1.0 - startRatio) * _width;
            float startOffsetX = (float)(startRatio * _width) - halfPanel;

            float totalSec;
            float p1, p2, p3, p4;

            var clipAnim = _compositor.CreateScalarKeyFrameAnimation();
            var dividerAnim = _compositor.CreateScalarKeyFrameAnimation();

            if (goRightFirst) {
                float toRightSec = (float)(1.0 - startRatio) * sweepSec;
                float toLeftSec = sweepSec;
                float backToStartSec = (float)startRatio * sweepSec;

                totalSec = toRightSec + pauseSec + toLeftSec + pauseSec + backToStartSec;
                p1 = toRightSec / totalSec;
                p2 = p1 + pauseSec / totalSec;
                p3 = p2 + toLeftSec / totalSec;
                p4 = p3 + pauseSec / totalSec;

                clipAnim.InsertKeyFrame(0f, startRightInset);
                clipAnim.InsertKeyFrame(p1, 0f, linear);
                clipAnim.InsertKeyFrame(p2, 0f);
                clipAnim.InsertKeyFrame(p3, _width, linear);
                clipAnim.InsertKeyFrame(p4, _width);
                clipAnim.InsertKeyFrame(1f, startRightInset, linear);

                dividerAnim.InsertKeyFrame(0f, startOffsetX);
                dividerAnim.InsertKeyFrame(p1, _width - halfPanel, linear);
                dividerAnim.InsertKeyFrame(p2, _width - halfPanel);
                dividerAnim.InsertKeyFrame(p3, -halfPanel, linear);
                dividerAnim.InsertKeyFrame(p4, -halfPanel);
                dividerAnim.InsertKeyFrame(1f, startOffsetX, linear);

                _animToRightEndSec = toRightSec;
                _animPause1EndSec = toRightSec + pauseSec;
                _animToLeftEndSec = toRightSec + pauseSec + toLeftSec;
                _animPause2EndSec = toRightSec + pauseSec + toLeftSec + pauseSec;
            }
            else {
                float toLeftSec = (float)startRatio * sweepSec;
                float toRightSec = sweepSec;
                float backToStartSec = (float)(1.0 - startRatio) * sweepSec;

                totalSec = toLeftSec + pauseSec + toRightSec + pauseSec + backToStartSec;
                p1 = toLeftSec / totalSec;
                p2 = p1 + pauseSec / totalSec;
                p3 = p2 + toRightSec / totalSec;
                p4 = p3 + pauseSec / totalSec;

                clipAnim.InsertKeyFrame(0f, startRightInset);
                clipAnim.InsertKeyFrame(p1, _width, linear);
                clipAnim.InsertKeyFrame(p2, _width);
                clipAnim.InsertKeyFrame(p3, 0f, linear);
                clipAnim.InsertKeyFrame(p4, 0f);
                clipAnim.InsertKeyFrame(1f, startRightInset, linear);

                dividerAnim.InsertKeyFrame(0f, startOffsetX);
                dividerAnim.InsertKeyFrame(p1, -halfPanel, linear);
                dividerAnim.InsertKeyFrame(p2, -halfPanel);
                dividerAnim.InsertKeyFrame(p3, _width - halfPanel, linear);
                dividerAnim.InsertKeyFrame(p4, _width - halfPanel);
                dividerAnim.InsertKeyFrame(1f, startOffsetX, linear);

                _animToRightEndSec = -1;
                _animPause1EndSec = toLeftSec + pauseSec;
                _animToLeftEndSec = toLeftSec;
                _animPause2EndSec = toLeftSec + pauseSec + toRightSec + pauseSec;
            }

            clipAnim.Duration = TimeSpan.FromSeconds(totalSec);
            clipAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            dividerAnim.Duration = TimeSpan.FromSeconds(totalSec);
            dividerAnim.IterationBehavior = AnimationIterationBehavior.Forever;

            // 设置初始延迟
            if (initialDelay.HasValue && initialDelay.Value > TimeSpan.Zero) {
                clipAnim.DelayTime = initialDelay.Value;
                clipAnim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                dividerAnim.DelayTime = initialDelay.Value;
                dividerAnim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            }

            _afterClip!.StartAnimation("RightInset", clipAnim);
            _dividerVisual!.StartAnimation("Offset.X", dividerAnim);

            _animRunning = true;
            _animTotalSec = totalSec;
            _animStartTime = DateTime.UtcNow;
            _lastDirectionIsRight = goRightFirst;
        }


        /// <summary>
        /// 根据动画启动时间和当前时间，推算此刻动画的运动方向。
        /// </summary>
        private bool InferCurrentDirection() {
            if (!_animRunning || _animTotalSec <= 0) return _lastDirectionIsRight;

            double elapsed = (DateTime.UtcNow - _animStartTime).TotalSeconds;
            double phase = elapsed % _animTotalSec;

            if (_lastDirectionIsRight) {
                // goRightFirst 路径：
                // [0, toRightEnd) → 向右
                // [toRightEnd, pause1End) → 停顿（视为将要向左）
                // [pause1End, toLeftEnd) → 向左
                // [toLeftEnd, pause2End) → 停顿（视为将要向右）
                // [pause2End, total) → 向右（回到起点）
                if (phase < _animToRightEndSec) return true;
                if (phase < _animPause1EndSec) return false; // 即将向左
                if (phase < _animToLeftEndSec) return false;
                if (phase < _animPause2EndSec) return true; // 即将向右
                return true;
            }
            else {
                // goLeftFirst 路径：
                // [0, toLeftEnd) → 向左
                // [toLeftEnd, pause1End) → 停顿（视为将要向右）
                // [pause1End, pause1End + sweepSec) → 向右
                // ... → 停顿（视为将要向左）
                // 最后阶段 → 向左（回到起点）
                if (phase < _animToLeftEndSec) return false;
                if (phase < _animPause1EndSec) return true;
                double toRightEndPhase = _animPause2EndSec - (float)PauseDuration.TotalSeconds;
                if (phase < toRightEndPhase) return true;
                if (phase < _animPause2EndSec) return false;
                return false;
            }
        }

        private void StopCompositionAnimation() {
            if (!_animRunning) return;

            // 在停止前推算方向
            _lastDirectionIsRight = InferCurrentDirection();

            _afterClip?.StopAnimation("RightInset");
            _dividerVisual?.StopAnimation("Offset.X");

            _animRunning = false;
        }

        private static void OnAutoAnimateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var self = (ImageCompareSlider)d;
            if ((bool)e.NewValue) {
                self.StartCompositionAnimation(self._currentRatio, self._lastDirectionIsRight);
            }
            else {
                self.StopCompositionAnimation();
                self.ApplyStaticPosition(self._currentRatio);
            }
        }

        #endregion

        #region Pointer Interaction

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e) {
            _isDragging = true;
            ((UIElement)sender).CapturePointer(e.Pointer);

            // 停止动画（内部会推算方向），切换到手动
            StopCompositionAnimation();

            UpdatePositionFromPointer(e);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e) {
            if (!_isDragging) return;
            UpdatePositionFromPointer(e);
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e) {
            if (!_isDragging) return;
            _isDragging = false;
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);

            if (AutoAnimate) {
                StartCompositionAnimation(_currentRatio, _lastDirectionIsRight);
            }

            e.Handled = true;
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e) {
            if (!_isDragging) return;
            _isDragging = false;

            if (AutoAnimate) {
                StartCompositionAnimation(_currentRatio, _lastDirectionIsRight);
            }
        }

        private void UpdatePositionFromPointer(PointerRoutedEventArgs e) {
            var point = e.GetCurrentPoint(RootGrid);
            _currentRatio = Math.Clamp(point.Position.X / _width, 0, 1);
            ApplyStaticPosition(_currentRatio);
        }

        #endregion

        #region Private fields
        /// <summary>
        /// 标记是否为首次加载，首次加载时动画延迟启动。
        /// </summary>
        private bool _isFirstLoad = true;
        private Compositor? _compositor;
        private Visual? _afterVisual;
        private Visual? _dividerVisual;
        private InsetClip? _afterClip;

        private bool _animRunning;
        private bool _isDragging;
        private double _currentRatio = 0.0;

        private float _width;
        private float _height;

        /// <summary>
        /// 记录动画被中断时的运动方向。
        /// true = 正在往右移动（ratio 递增），false = 正在往左移动（ratio 递减）。
        /// </summary>
        private bool _lastDirectionIsRight = true;

        /// <summary>
        /// 记录动画启动时的时间戳，用于推算中断时的方向。
        /// </summary>
        private DateTime _animStartTime;

        /// <summary>
        /// 保存动画各阶段的时间节点，用于推算中断时的方向。
        /// </summary>
        private double _animTotalSec;
        private double _animToRightEndSec;
        private double _animPause1EndSec;
        private double _animToLeftEndSec;
        private double _animPause2EndSec;

        #endregion
    }
}