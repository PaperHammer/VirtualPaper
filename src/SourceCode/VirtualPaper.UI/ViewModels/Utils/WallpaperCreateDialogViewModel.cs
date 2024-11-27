using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.UI;
using VirtualPaper.UIComponent.Utils;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels.Utils {
    public partial class WallpaperCreateDialogViewModel : ObservableObject {
        public List<WallpaperCreateData> WallpaperCategoriesFiltered { get; set; }

        private WallpaperCreateData _selectedItem;
        public WallpaperCreateData SelectedItem {
            get => _selectedItem;
            set { _selectedItem = value; OnPropertyChanged(); }
        }

        public WallpaperCreateDialogViewModel() {
            _localizer = LanguageUtil.LocalizerInstacne;

            InitCollections();
        }

        private void InitCollections() {
            WallpaperCategoriesFiltered = [
                new WallpaperCreateData() {
                    Title = _localizer.GetLocalizedString(Constants.LocalText.WpCreateDialog_CommonWp_Title),
                    Description = _localizer.GetLocalizedString(Constants.LocalText.WpCreateDialog_CommonWp_Explain),
                    Icon = "../../Assets/icons8-image-96.png",
                    CreateType = WallpaperCreateType.Img,
                },
                new WallpaperCreateData() {
                    Title = _localizer.GetLocalizedString(Constants.LocalText.WpCreateDialog_AIWp_Title),
                    Description = _localizer.GetLocalizedString(Constants.LocalText.WpCreateDialog_AIWp_Explain),
                    Icon = "../../Assets/icons8-picture-94.png",
                    CreateType = WallpaperCreateType.DepthImg,
                }
            ];
            SelectedItem = WallpaperCategoriesFiltered[0];
        }

        private readonly ILocalizer _localizer;
    }
}
