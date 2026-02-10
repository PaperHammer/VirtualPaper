using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.UIComponent.Attributes;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Utils.Extensions {
    public static class ArcNavigationContentViewExtension {
        public static void ArcNavigate(
            this ArcNavigationContentView navView,
            Type targetPageType,
            FrameworkPayload? payload = null,
            ArcNavigationOptions? options = null) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                if (!typeof(ArcPage).IsAssignableFrom(targetPageType))
                    throw new InvalidOperationException($"{targetPageType.Name} is not ArcPage");

                ArcPage? oldPage = GetActivePage(navView.PageMap);
                ArcPage newPage = GetBackgroundPage(navView.PageMap, targetPageType) ?? ResolvePageInstance(targetPageType);

                bool useAnimation = options != null;
                if (!useAnimation) NavigateRootWithoutAnimation(navView, oldPage, newPage, payload);
                else NavigateRootWithAnimation(navView, oldPage, newPage, payload, options!.Transition);
            });
        }

        private static void NavigateRootWithoutAnimation(
            ArcNavigationContentView navView,
            ArcPage? oldPage,
            ArcPage newPage,
            FrameworkPayload? paylaod) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                if (!navView.PageMap.ContainsKey(newPage.ArcType)) {
                    navView.PageMap[newPage.ArcType] = newPage;
                    navView.ContentGrid.Children.Add(newPage);
                }
                newPage.NavigateEnter(paylaod);
                
                HandlePageStatus(navView, oldPage, newPage);
            });
        }

        private static void NavigateRootWithAnimation(
            ArcNavigationContentView navView,
            ArcPage? oldPage,
            ArcPage newPage,
            FrameworkPayload? parameter,
            ArcNavigationTransition transition) {
            newPage.Opacity = 0;
            if (!navView.PageMap.ContainsKey(newPage.ArcType)) {
                navView.PageMap[newPage.ArcType] = newPage;
                navView.ContentGrid.Children.Add(newPage);
            }
            newPage.NavigateEnter(parameter);
            
            HandlePageStatus(navView, oldPage, newPage);
            
            PlayEnterAnimation(newPage, transition);
        }

        private static void HandlePageStatus(ArcNavigationContentView navView, ArcPage? oldPage, ArcPage newPage) {
            if (oldPage == null) {
                newPage.SetActiveStatus();
                return;
            }

            if (ReferenceEquals(oldPage, newPage)) {
                newPage.SetActiveStatus();
                return;
            }

            oldPage.NavigateExit(
                beforeLeave: () => {
                    // 如果在等待期间 oldPage 被复活（变为 PreActive 或 Active），不能移除
                    if (oldPage.Status == ArcPageStatus.Active || oldPage.Status == ArcPageStatus.PreActive) {
                        return;
                    }

                    navView.PageMap.Remove(oldPage.ArcType);
                    CrossThreadInvoker.InvokeOnUIThread(() => {
                        if (navView.ContentGrid.Children.Contains(oldPage)) {
                            navView.ContentGrid.Children.Remove(oldPage);
                        }
                    });
                },
                afterDestoried: () => {
                    newPage.SetActiveStatus();
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
                var ctx = ArcPageContextManager.GetContext(type);
                if (ctx?.PageInstance is ArcPage page)
                    return page;
            }

            return Activator.CreateInstance(type) as ArcPage
                   ?? throw new Exception($"Cannot create page {type.Name}");
        }

        private static ArcPage? GetActivePage(Dictionary<Type, ArcPage> map) {
            foreach (var kvp in map) {
                // 在快速切换时，上一个页面可能还处于 PreActive（动画中），它也应该被视为 oldPage
                if (kvp.Value.Status == ArcPageStatus.Active || kvp.Value.Status == ArcPageStatus.PreActive) {
                    return kvp.Value;
                }
            }

            return null;
        }

        private static ArcPage? GetBackgroundPage(Dictionary<Type, ArcPage> map, Type targetPageType) {
            foreach (var kvp in map) {
                if (kvp.Value.ArcType == targetPageType && kvp.Value.Status == ArcPageStatus.BackgroundRunning) {
                    return kvp.Value;
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
