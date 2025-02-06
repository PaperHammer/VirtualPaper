using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using Windows.Storage;
using WinUI3Localizer;

namespace VirtualPaper.UIComponent.Utils {
    public class LanguageUtil {
        public static ILocalizer LocalizerInstacne { get; private set; }

        static LanguageUtil() {
            SetInstance();
        }

        #region load language       
        public static async void LanguageChanged(string lang) {
            await Localizer.Get().SetLanguage(lang);
            SetInstance();
        }

        public static string GetI18n(string key) {
            return LocalizerInstacne.GetLocalizedString(key);
        }

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
            SetInstance();
        }

        public static async Task InitializeLocalizerForPackaged(string lang) {
            // Initialize a "Strings" folder in the "LocalFolder" for the packaged app.
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFolder stringsFolder = await localFolder.CreateFolderAsync(
              "Strings",
               CreationCollisionOption.OpenIfExists);

            // Create string resources file from app resources if doesn't exists.
            string resourceFileName = "Resources.resw";
            await CreateStringResourceFileIfNotExists(stringsFolder, "zh-CN", resourceFileName);
            await CreateStringResourceFileIfNotExists(stringsFolder, "en-US", resourceFileName);

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolder.Path)
                .SetOptions(options => {
                    options.DefaultLanguage = lang;
                })
                .Build();
            SetInstance();
        }

        private static async Task CreateStringResourceFileIfNotExists(StorageFolder stringsFolder, string language, string resourceFileName) {
            StorageFolder languageFolder = await stringsFolder.CreateFolderAsync(
                language,
                CreationCollisionOption.OpenIfExists);

            if (await languageFolder.TryGetItemAsync(resourceFileName) is null) {
                string resourceFilePath = Path.Combine(stringsFolder.Name, language, resourceFileName);
                StorageFile resourceFile = await LoadStringResourcesFileFromAppResource(resourceFilePath);
                _ = await resourceFile.CopyAsync(languageFolder);
            }
        }

        private static async Task<StorageFile> LoadStringResourcesFileFromAppResource(string filePath) {
            Uri resourcesFileUri = new($"ms-appx:///{filePath}");
            return await StorageFile.GetFileFromApplicationUriAsync(resourcesFileUri);
        }

        private static void SetInstance() {
            LocalizerInstacne = Localizer.Get();
        }
        #endregion
    }
}
