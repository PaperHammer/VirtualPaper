using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UI.Services.Interfaces;
using WinUIEx.Messaging;
using static VirtualPaper.UI.Services.Interfaces.IDialogService;

namespace VirtualPaper.UI.Services {
    public class DialogService : IDialogService {
        public async Task ShowDialogAsync(
            string message,
            string title,
            string primaryBtnText) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = primaryBtnText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.Services.GetRequiredService<MainWindow>().Content.XamlRoot,
            };

            await dialog.ShowAsync();
        }

        public async Task<DialogResult> ShowDialogAsync(
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
                XamlRoot = App.Services.GetRequiredService<MainWindow>().Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Seconday,
                _ => DialogResult.None,
            };
        }

        public async Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Title = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap },
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = App.Services.GetRequiredService<MainWindow>().Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Seconday,
                _ => DialogResult.None,
            };
        }

        public async Task<DialogResult> ShowDialogWithoutTitleAsync(
            object content,
            string primaryBtnText,
            bool isDefaultPrimary = true) {
            var dialog = new ContentDialog() {
                Content = content is string message ? new TextBlock() { Text = message, TextWrapping = TextWrapping.Wrap } : content,
                PrimaryButtonText = primaryBtnText,
                DefaultButton = isDefaultPrimary ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
                XamlRoot = App.Services.GetRequiredService<MainWindow>().Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();

            return result switch {
                ContentDialogResult.None => DialogResult.None,
                ContentDialogResult.Primary => DialogResult.Primary,
                ContentDialogResult.Secondary => DialogResult.Seconday,
                _ => DialogResult.None,
            };
        }
    }
}
