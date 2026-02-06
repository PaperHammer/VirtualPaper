using System;
using System.Collections.Generic;
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
            NavigationPayload? parameter = null,
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
            NavigationPayload? parameter) {
            navView.ContentFrame.Content = null;
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
            NavigationPayload? parameter,
            ArcNavigationTransition transition) {
            navView.ContentFrame.Content = null;
            newPage.Opacity = 0;
            navView.ContentFrame.Content = newPage;
            newPage.NavigateEnter(parameter);

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
                var ctx = ArcPageContextManager.GetContext(type);
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

    public sealed class NavigationPayload {
        public object? this[string key] {
            set => Set(key, value);
        }

        public void Set(string key, object? value) {
            _data[key] = value;
        }

        public bool TryGet<T>(string key, out T value) {
            if (_data.TryGetValue(key, out var obj) && obj is T t) {
                value = t;
                return true;
            }

            value = default!;
            return false;
        }

        public T Get<T>(string key) {
            if (_data.TryGetValue(key, out var value) && value is T t)
                return t;

            throw new KeyNotFoundException($"NavigationPayload missing required key '{key}' ({typeof(T).Name})");
        }

        public bool ContainsKey(string key) {
            return _data.ContainsKey(key);
        }

        public bool ContainsKey(NaviPayloadKey key) {
            return _data.ContainsKey(key.ToString());
        }

        public IReadOnlyDictionary<string, object?> GetRawData() {
            return _data;
        }

        private readonly Dictionary<string, object?> _data = [];
    }

    public static class NavigationPayloadExtensions {
        public static NavigationPayload AddRange(this NavigationPayload payload, params NaviPayloadData[] items) {
            return payload.AddRange(true, items);
        }

        public static NavigationPayload AddRange(this NavigationPayload payload, bool overwrite, params NaviPayloadData[] items) {
            if (items is null) return payload;

            foreach (var item in items) {
                string keyStr = item.Key.ToString();
                if (!overwrite && payload.ContainsKey(keyStr)) {
                    continue;
                }
                payload.Set(keyStr, item.Value);
            }

            return payload;
        }

        public static NaviPayloadData[] ToArray(this NavigationPayload payload) {
            if (payload == null) return [];

            var list = new List<NaviPayloadData>();
            var rawData = payload.GetRawData();

            foreach (var kvp in rawData) {
                if (Enum.TryParse<NaviPayloadKey>(kvp.Key, out var enumKey)) {
                    list.Add(new NaviPayloadData(enumKey, kvp.Value!));
                }
            }

            return list.ToArray();
        }

        public static NavigationPayload Merge(this NavigationPayload target, NavigationPayload? source, bool overwrite = true) {
            if (target is null) return null!;
            if (source is null) return target;

            foreach (var kvp in source.GetRawData()) {
                if (!overwrite && target.ContainsKey(kvp.Key)) {
                    continue;
                }
                target.Set(kvp.Key, kvp.Value);
            }

            return target;
        }
    }

    public record NaviPayloadData(NaviPayloadKey Key, object Value);

    public enum NaviPayloadKey {
        OnlyDetails,
        PreviewWithWeb,
        IEffectService,
        StartArgs,
        AvailableConfigTab,
        IWpBasicData,
        IIpcObserver,
        ArcWindow,
        ApplyService,
        ConfigSpacePage,
        RecentUsedFiles,
        LocalFiles,
        DraftPage,
        Project,
        ICardComponent,
    }
}
