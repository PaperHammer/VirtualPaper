using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.WallpaperMetaData;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels
{
    internal class DetailedInfoViewModel : ObservableObject
    {
        public string VirtualPaperUid { get; set; } = string.Empty;

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set { _title = value; _metaData.Title = value; OnPropertyChanged(); }
        }

        private string _desc = string.Empty;
        public string Desc
        {
            get => _desc;
            set { _desc = value; _metaData.Desc = value; OnPropertyChanged(); }
        }

        public string Type { get; set; } = string.Empty;
        public string Authors { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;

        private string _tags = string.Empty;
        public string Tags
        {
            get => _tags;
            set { _tags = value; _metaData.Tags = value; OnPropertyChanged(); }
        }

        public string Resolution { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;

        public string DetailedInfo_TextVirtualPaperUid { get; init; } = string.Empty;
        public string DetailedInfo_TextTtile { get; init; } = string.Empty;
        public string DetailedInfo_TextDesc { get; init; } = string.Empty;
        public string DetailedInfo_TextType { get; init; } = string.Empty;
        public string DetailedInfo_TextAuthors { get; init; } = string.Empty;
        public string DetailedInfo_TextPublishDate { get; init; } = string.Empty;
        public string DetailedInfo_TextTags { get; init; } = string.Empty;
        public string DetailedInfo_TextResolution { get; init; } = string.Empty;
        public string DetailedInfo_TextAspectRadio { get; init; } = string.Empty;
        public string DetailedInfo_TextFileExtension { get; init; } = string.Empty;
        public string DetailedInfo_TextFileSize { get; init; } = string.Empty;
        public bool IsEditable { get; set; } = false;

        public DetailedInfoViewModel(IMetaData metaData, bool isEditable = false)
        {
            if (metaData == null) return;
            _metaData = metaData;

            _localizer = Localizer.Get();

            this.DetailedInfo_TextVirtualPaperUid = _localizer.GetLocalizedString("DetailedInfo_TextVirtualPaperUid");
            this.DetailedInfo_TextTtile = _localizer.GetLocalizedString("DetailedInfo_TextTtile");
            this.DetailedInfo_TextDesc = _localizer.GetLocalizedString("DetailedInfo_TextDesc");
            this.DetailedInfo_TextType = _localizer.GetLocalizedString("DetailedInfo_TextType");
            this.DetailedInfo_TextAuthors = _localizer.GetLocalizedString("DetailedInfo_TextAuthors");
            this.DetailedInfo_TextPublishDate = _localizer.GetLocalizedString("DetailedInfo_TextPublishDate");
            this.DetailedInfo_TextTags = _localizer.GetLocalizedString("DetailedInfo_TextTags");
            this.DetailedInfo_TextResolution = _localizer.GetLocalizedString("DetailedInfo_TextResolution");
            this.DetailedInfo_TextAspectRadio = _localizer.GetLocalizedString("DetailedInfo_TextAspectRadio");
            this.DetailedInfo_TextFileExtension = _localizer.GetLocalizedString("DetailedInfo_TextFileExtension");
            this.DetailedInfo_TextFileSize = _localizer.GetLocalizedString("DetailedInfo_TextFileSize");

            this.VirtualPaperUid = metaData.VirtualPaperUid;
            this.Title = metaData.Title;
            this.Desc = metaData.Desc;
            this.Type = metaData.Type.ToString();
            this.Authors = string.Join(",", metaData.Authors);
            this.PublishDate = metaData.PublishDate;
            this.Tags = string.Join(",", metaData.Tags);
            this.Resolution = metaData.Resolution;
            this.AspectRatio = metaData.AspectRatio;
            this.FileExtension = metaData.FileExtension;
            this.FileSize = metaData.FileSize;
            this.IsEditable = isEditable;
        }

        private ILocalizer _localizer;
        private IMetaData _metaData;
    }
}
