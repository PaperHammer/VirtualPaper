using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;

namespace VirtualPaper.UIComponent.Utils {
    public static class GlobalDialogUtils {
        public static async Task ShowDialogAsync(
            string message,
            string title,
            string primaryBtnText) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = primaryBtnText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            await dialog.ShowAsync();
        }

        public static async Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                SecondaryButtonText = secondaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Secondary,
                _ => DialogResult.None,
            };
        }
        
        public static ContentDialog? CreateDialog(
            object content,
            string title,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                SecondaryButtonText = secondaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            return dialog;
        }

        public static async Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Secondary,
                _ => DialogResult.None,
            };
        }
        
        public static async Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            string secondaryBtnText,
            string closeBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                SecondaryButtonText = secondaryBtnText,
                CloseButtonText = closeBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Secondary,
                _ => DialogResult.None,
            };
        }
        
        public static ContentDialog? CreateDialog(
            object content,
            string title,
            string primaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            return dialog;
        }

        public static async Task<DialogResult> ShowDialogWithoutTitleAsync(
            object content,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                SecondaryButtonText = secondaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Secondary,
                _ => DialogResult.None,
            };
        }
        
        public static ContentDialog? CreateDialogWithoutTitle(
            object content,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                SecondaryButtonText = secondaryBtnText,                
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            return dialog;
        }

        public static async Task<DialogResult> ShowDialogWithoutTitleAsync(
            object content,
            string primaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Secondary,
                _ => DialogResult.None,
            };
        }

        public static ContentDialog? CreateDialogWithoutTitle(
            object content,
            string primaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = ArcWindowManager.MainWindow.Content.XamlRoot,
                RequestedTheme = ArcWindowManager.MainWindow.ContentHost.AppRoot.RequestedTheme,
            };

            return dialog;
        }
    }
}
