using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using Windows.Storage;
using WinUI3Localizer;

namespace VirtualPaper.UIComponent.Utils {
    public class LanguageUtil {
        public static ILocalizer LocalizerInstance { get; private set; }
        public static string CurrentLanguage { get; private set; }

        #region load language       
        public static event EventHandler? LanguageUpdated;

        public static async Task SetLanguageAsync(string lang) {
            await Localizer.Get().SetLanguage(lang);
            SetInstance(lang);
            LanguageUpdated?.Invoke(null, EventArgs.Empty);
        }

        public static string GetI18n(string key) =>
            LocalizerInstance?.GetLocalizedString(key) ?? key;

        // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
        public static async Task InitializeLocalizerForUnpackaged(string lang) {
            // Initialize a "Strings" folder in the executables folder.
            string stringsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ModuleName.UIComponent, "Strings");
            StorageFolder stringsFolder = await StorageFolder.GetFolderFromPathAsync(stringsFolderPath);

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                .SetOptions(options => {
                    options.DefaultLanguage = lang;
                })
                .Build();
            await localizer.SetLanguage(lang);
            SetInstance(lang);
        }

        public static async Task InitializeLocalizerForPackaged(string lang) {
            // Initialize a "Strings" folder in the "LocalFolder" for the packaged app.
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFolder stringsFolder = await localFolder.CreateFolderAsync(
              "Strings",
               CreationCollisionOption.OpenIfExists);

            // Refresh bundled translations; LocalFolder survives application upgrades.
            string resourceFileName = "Resources.resw";
            await RefreshStringResourceFileAsync(stringsFolder, "zh-CN", resourceFileName);
            await RefreshStringResourceFileAsync(stringsFolder, "en-US", resourceFileName);

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolder.Path)
                .SetOptions(options => {
                    options.DefaultLanguage = lang;
                })
                .Build();
            await localizer.SetLanguage(lang);
            SetInstance(lang);
        }

        private static async Task RefreshStringResourceFileAsync(StorageFolder stringsFolder, string language, string resourceFileName) {
            StorageFolder languageFolder = await stringsFolder.CreateFolderAsync(
                language,
                CreationCollisionOption.OpenIfExists);

            // LocalFolder survives upgrades; load the currently bundled translations.
            string resourceFilePath = $"Strings/{language}/{resourceFileName}";
            StorageFile resourceFile = await LoadStringResourcesFileFromAppResource(resourceFilePath);
            _ = await resourceFile.CopyAsync(languageFolder, resourceFileName, NameCollisionOption.ReplaceExisting);
        }

        private static async Task<StorageFile> LoadStringResourcesFileFromAppResource(string filePath) {
            Uri resourcesFileUri = new($"ms-appx:///{filePath}");
            return await StorageFile.GetFileFromApplicationUriAsync(resourcesFileUri);
        }

        private static void SetInstance(string lang) {
            CurrentLanguage = lang;
            LocalizerInstance = Localizer.Get();
        }
        #endregion

    }
}
