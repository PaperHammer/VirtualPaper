using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Models.Cores.Interfaces;
using Windows.UI.ViewManagement;

namespace VirtualPaper.UIComponent.Navigation {
    public static class NavigationAnimation {
        public static bool IsEnabled(ISettings settings) =>
            settings.NavigationAnimationsEnabled && new UISettings().AnimationsEnabled;

        public static NavigationTransitionInfo CreateTransition(
            ISettings settings, SlideNavigationTransitionEffect? slideEffect = null) {
            if (!IsEnabled(settings)) return new SuppressNavigationTransitionInfo();
            return slideEffect.HasValue
                ? new SlideNavigationTransitionInfo { Effect = slideEffect.Value }
                : new EntranceNavigationTransitionInfo();
        }

        public static void GoForward(Frame frame, ISettings settings) {
            var next = frame.ForwardStack[0];
            frame.ForwardStack[0] = new PageStackEntry(next.SourcePageType, next.Parameter, CreateTransition(settings));
            frame.GoForward();
        }
    }
}
