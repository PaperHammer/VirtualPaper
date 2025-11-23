using System;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class ArcNavigationContentViewExtension {
        public static bool ArcNavigate(
            this ArcNavigationContentView navView,
            Grid keepAliveBuffer,
            Type targetPageType,
            object? parameter = null,
            ArcNavigationOptions? options = null) {
            if (!typeof(ArcPage).IsAssignableFrom(targetPageType))
                throw new InvalidOperationException($"{targetPageType.Name} is not ArcPage.");

            ArcPage? oldPage = navView.ContentFrame.Content as ArcPage;
            ArcPage newPage = TryWakeUpPageFromBuffer(keepAliveBuffer, targetPageType)
                              ?? ResolvePageInstance(targetPageType);

            bool useAnimation = options != null;
            if (!useAnimation)
                return NavigateRootWithoutAnimation(navView, keepAliveBuffer, oldPage, newPage, parameter);

            return NavigateRootWithAnimation(navView, keepAliveBuffer, oldPage, newPage, parameter, options!.Transition);
        }

        private static bool NavigateRootWithoutAnimation(
            ArcNavigationContentView navView,
            Grid keepAliveBuffer,
            ArcPage? oldPage,
            ArcPage newPage,
            object? parameter) {
            navView.ContentFrame.Content = newPage;
            newPage.NavigateEnter(parameter);

            if (oldPage != null)
                MoveToBufferAndExit(navView, keepAliveBuffer, oldPage);

            return true;
        }

        private static bool NavigateRootWithAnimation(
            ArcNavigationContentView navView,
            Grid keepAliveBuffer,
            ArcPage? oldPage,
            ArcPage newPage,
            object? parameter,
            ArcNavigationTransition transition) {
            navView.ContentFrame.Content = null;

            newPage.NavigateEnter(parameter);
            newPage.Opacity = 0;
            navView.ContentFrame.Content = newPage;

            if (oldPage != null)
                MoveToBufferAndExit(navView, keepAliveBuffer, oldPage);

            PlayEnterAnimation(newPage, transition);

            return true;
        }

        private static void MoveToBufferAndExit(ArcNavigationContentView navView, Grid bufferGrid, ArcPage oldPage) {
            navView.PageBufferMap[oldPage.PageType] = oldPage;
            bufferGrid.Children.Add(oldPage);

            oldPage.NavigateExit(() => {
                CrossThreadInvoker.InvokeOnUIThread(() => {
                    bufferGrid.Children.Remove(oldPage);
                    navView.PageBufferMap.Remove(oldPage.PageType);
                });
            });
        }

        private static void PlayEnterAnimation(UIElement newPage, ArcNavigationTransition transition) {
            var sb = new Storyboard();

            switch (transition) {
                case ArcNavigationTransition.Fade:
                    var fade = new DoubleAnimation {
                        From = 0,
                        To = 1,
                        Duration = FadeAnimationCache.Duration,
                        EasingFunction = FadeAnimationCache.Ease
                    };
                    Storyboard.SetTarget(fade, newPage);
                    Storyboard.SetTargetProperty(fade, "Opacity");
                    sb.Children.Add(fade);
                    break;

                default:
                    throw new NotImplementedException();
            }

            sb.Begin();
        }

        private static ArcPage ResolvePageInstance(Type type) {
            bool keepAlive = type.GetCustomAttribute<KeepAliveAttribute>()?.Value == true;

            if (keepAlive) {
                var ctx = PageContextManager.GetContext(type);
                if (ctx?.PageInstance is ArcPage page)
                    return page;
            }

            return Activator.CreateInstance(type) as ArcPage
                   ?? throw new Exception($"Cannot create page {type.Name}");
        }

        private static ArcPage? TryWakeUpPageFromBuffer(Grid bufferGrid, Type targetPageType) {
            foreach (var child in bufferGrid.Children) {
                if (child is ArcPage page && page.GetType() == targetPageType) {
                    if (!page.IsPreLeaved) {
                        bufferGrid.Children.Remove(page);
                        return page;
                    }
                }
            }

            return null;
        }
    }

    public enum ArcNavigationTransition {
        Fade,
        // Slide, DrillIn ...
    }

    public record ArcNavigationOptions(ArcNavigationTransition Transition);

    static class FadeAnimationCache {
        public const int DurationMs = 250;

        public static readonly Duration Duration =
            new(TimeSpan.FromMilliseconds(DurationMs));

        public static readonly ExponentialEase Ease =
            new() { EasingMode = EasingMode.EaseOut };

        public static DoubleAnimation CreateFadeIn() {
            return new DoubleAnimation {
                From = 0,
                To = 1,
                Duration = Duration,
                EasingFunction = Ease
            };
        }

        public static DoubleAnimation CreateFadeOut() {
            return new DoubleAnimation {
                From = 1,
                To = 0,
                Duration = Duration,
                EasingFunction = Ease
            };
        }
    }
}
