using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Models;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.UI.ViewModels.Utils
{
    public partial class WallpaperCreateDialogViewModel : ObservableObject {
        public List<WallpaperCreateData> WallpaperCategoriesFiltered { get; set; }

        private WallpaperCreateData _selectedItem;
        public WallpaperCreateData SelectedItem {
            get => _selectedItem;
            set { _selectedItem = value; OnPropertyChanged(); }
        }

        public WallpaperCreateDialogViewModel() {
            InitCollections();
        }

        private void InitCollections() {
            WallpaperCategoriesFiltered = [
                new WallpaperCreateData() {
                    Title = App.GetI18n(Constants.I18n.WpCreateDialog_CommonWp_Title),
                    Description = App.GetI18n(Constants.I18n.WpCreateDialog_CommonWp_Explain),
                    Icon = "../../Assets/icons8-image-96.png",
                    CreateType = WallpaperCreateType.Img,
                },
                new WallpaperCreateData() {
                    Title = App.GetI18n(Constants.I18n.WpCreateDialog_AIWp_Title),
                    Description = App.GetI18n(Constants.I18n.WpCreateDialog_AIWp_Explain),
                    Icon = "../../Assets/icons8-picture-94.png",
                    CreateType = WallpaperCreateType.DepthImg,
                }
            ];
            SelectedItem = WallpaperCategoriesFiltered[0];
        }
    }
}
