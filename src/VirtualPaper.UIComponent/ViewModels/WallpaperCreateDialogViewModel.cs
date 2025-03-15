using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Models;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.ViewModels {
    public partial class WallpaperCreateViewModel : ObservableObject {
        public List<WallpaperCreateData> WallpaperCategoriesFiltered { get; set; }

        private WallpaperCreateData _selectedItem;
        public WallpaperCreateData SelectedItem {
            get => _selectedItem;
            set { _selectedItem = value; OnPropertyChanged(); }
        }

        public WallpaperCreateViewModel() {
            InitCollections();
        }

        private void InitCollections() {
            WallpaperCategoriesFiltered = [
                new WallpaperCreateData() {
                    Title = LanguageUtil.GetI18n(Constants.I18n.WpCreateDialog_CommonWp_Title),
                    Description = LanguageUtil.GetI18n(Constants.I18n.WpCreateDialog_CommonWp_Explain),
                    Icon = "ms-appx:///Assets/icons8-image-96.png",
                    CreateType = WallpaperCreateType.Img,
                },
                new WallpaperCreateData() {
                    Title = LanguageUtil.GetI18n(Constants.I18n.WpCreateDialog_AIWp_Title),
                    Description = LanguageUtil.GetI18n(Constants.I18n.WpCreateDialog_AIWp_Explain),
                    Icon = "ms-appx:///Assets/icons8-picture-94.png",
                    CreateType = WallpaperCreateType.DepthImg,
                }
            ];
            SelectedItem = WallpaperCategoriesFiltered[0];
        }
    }
}
