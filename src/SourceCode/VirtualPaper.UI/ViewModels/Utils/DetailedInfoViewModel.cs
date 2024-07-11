using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.UI.Services.Interfaces;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels.Utils
{
    public class DetailedInfoViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public double Rating { get; set; } = -1;
        public double RatingShow { get; set; } = 0;
        public string Authors { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string VirtualPaperUid { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;

        public string DetailedInfo_TextScore { get; init; } = string.Empty;
        public string DetailedInfo_TextScoreTilte { get; init; } = string.Empty;
        public string DetailedInfo_TextScoreSubmit { get; init; } = string.Empty;
        public string DetailedInfo_TextPreview { get; init; } = string.Empty;
        public string DetailedInfo_TextDownload { get; init; } = string.Empty;
        public string DetailedInfo_TextRating { get; init; } = string.Empty;
        public string DetailedInfo_TextAuthors { get; init; } = string.Empty;
        public string DetailedInfo_TextDesc { get; init; } = string.Empty;
        public string DetailedInfo_TextVirtualPaperUid { get; init; } = string.Empty;
        public string DetailedInfo_TextType { get; init; } = string.Empty;
        public string DetailedInfo_TextPublishDate { get; init; } = string.Empty;
        public string DetailedInfo_TextTags { get; init; } = string.Empty;
        public string DetailedInfo_TextResolution { get; init; } = string.Empty;
        public string DetailedInfo_TextAspectRadio { get; init; } = string.Empty;
        public string DetailedInfo_TextFileExtension { get; init; } = string.Empty;
        public string DetailedInfo_TextFileSize { get; init; } = string.Empty;
        public bool EditEnable { get; set; } = false;

        public Visibility BtnScoreVisibility { get; set; }
        public Visibility BtnDownloadVisibility { get; set; }

        public DetailedInfoViewModel(
            IMetaData metaData, 
            bool editEnable,
            bool scoredEnable,
            bool downloadEnable)
        {
            if (metaData == null) return;

            _localizer = Localizer.Get();
            this._metaData = metaData;
            this._dialogService = App.Services.GetRequiredService<IDialogService>();
            this._wallpaperControlClient = App.Services.GetRequiredService<IWallpaperControlClient>();

            this.DetailedInfo_TextScore = _localizer.GetLocalizedString("DetailedInfo_TextScore");
            this.DetailedInfo_TextScoreTilte = _localizer.GetLocalizedString("DetailedInfo_TextScoreTilte");
            this.DetailedInfo_TextScoreSubmit = _localizer.GetLocalizedString("DetailedInfo_TextScoreSubmit");
            this.DetailedInfo_TextPreview = _localizer.GetLocalizedString("DetailedInfo_TextPreview");
            this.DetailedInfo_TextDownload = _localizer.GetLocalizedString("DetailedInfo_TextDownload");
            this.DetailedInfo_TextRating = _localizer.GetLocalizedString("DetailedInfo_Rating");

            this.DetailedInfo_TextVirtualPaperUid = _localizer.GetLocalizedString("DetailedInfo_TextVirtualPaperUid");
            this.DetailedInfo_TextType = _localizer.GetLocalizedString("DetailedInfo_TextType");
            this.DetailedInfo_TextAuthors = _localizer.GetLocalizedString("DetailedInfo_TextAuthors");
            this.DetailedInfo_TextDesc = _localizer.GetLocalizedString("DetailedInfo_TextDesc");
            this.DetailedInfo_TextPublishDate = _localizer.GetLocalizedString("DetailedInfo_TextPublishDate");
            this.DetailedInfo_TextTags = _localizer.GetLocalizedString("DetailedInfo_TextTags");
            this.DetailedInfo_TextResolution = _localizer.GetLocalizedString("DetailedInfo_TextResolution");
            this.DetailedInfo_TextAspectRadio = _localizer.GetLocalizedString("DetailedInfo_TextAspectRadio");
            this.DetailedInfo_TextFileExtension = _localizer.GetLocalizedString("DetailedInfo_TextFileExtension");
            this.DetailedInfo_TextFileSize = _localizer.GetLocalizedString("DetailedInfo_TextFileSize");

            this.VirtualPaperUid = metaData.VirtualPaperUid;
            this.ThumbnailPath = metaData.ThumbnailPath;
            this.Rating = metaData.Rating;
            this.RatingShow = Math.Max(0, this.Rating);
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
            
            this.EditEnable = editEnable;
            this.BtnScoreVisibility = VirtualPaperUid.StartsWith("VP") && scoredEnable 
                ? Visibility.Visible : Visibility.Collapsed;
            this.BtnDownloadVisibility = downloadEnable
                ? Visibility.Visible : Visibility.Collapsed;
        }

        internal async Task PreviewAsync()
        {
            await _wallpaperControlClient.PreviewWallpaperAsync(_metaData, true);
        }

        //internal async Task SubmitSocreAsync(double rating)
        //{
        //    throw new NotImplementedException();
        //}

        private ILocalizer _localizer;
        private IMetaData _metaData;
        private IDialogService _dialogService;
        private IWallpaperControlClient _wallpaperControlClient;
    }
}
