using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UIComponent.Utils;
using WinUI3Localizer;

namespace VirtualPaper.UI.ViewModels.Utils {
    public class DetailedInfoViewModel {
        public string Title { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public double Rating { get; set; } = -1;
        public double RatingShow { get; set; } = 0;
        public string Authors { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string WallPaperUid { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string TextVersion { get; set; } = string.Empty;

        public string DetailedInfo_TextScore { get; set; } = string.Empty;
        public string DetailedInfo_TextScoreTilte { get; set; } = string.Empty;
        public string DetailedInfo_TextScoreSubmit { get; set; } = string.Empty;
        public string DetailedInfo_TextPreview { get; set; } = string.Empty;
        public string DetailedInfo_TextDownload { get; set; } = string.Empty;
        public string DetailedInfo_TextRating { get; set; } = string.Empty;
        public string DetailedInfo_TextAuthors { get; set; } = string.Empty;
        public string DetailedInfo_TextDesc { get; set; } = string.Empty;
        public string DetailedInfo_TextVirtualPaperUid { get; set; } = string.Empty;
        public string DetailedInfo_TextRType { get; set; } = string.Empty;
        public string DetailedInfo_TextPublishDate { get; set; } = string.Empty;
        public string DetailedInfo_TextTags { get; set; } = string.Empty;
        public string DetailedInfo_TextResolution { get; set; } = string.Empty;
        public string DetailedInfo_TextAspectRadio { get; set; } = string.Empty;
        public string DetailedInfo_TextFileExtension { get; set; } = string.Empty;
        public string DetailedInfo_TextFileSize { get; set; } = string.Empty;
        public string DetailedInfo_TextVersionInfo { get; set; } = string.Empty;
        public bool EditEnable { get; set; } = false;

        public Visibility BtnScoreVisibility { get; set; }
        public Visibility BtnDownloadVisibility { get; set; }

        public DetailedInfoViewModel() {
            this._dialogService = App.Services.GetRequiredService<IDialogService>();
            this._wpControlClient = App.Services.GetRequiredService<IWallpaperControlClient>();
            
            _localizer = LanguageUtil.LocalizerInstacne;
            
            InitText();
        }

        private void InitText() {


            this.DetailedInfo_TextScore = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextScore);
            this.DetailedInfo_TextScoreTilte = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextScoreTilte);
            this.DetailedInfo_TextScoreSubmit = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextScoreSubmit);
            this.DetailedInfo_TextPreview = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextPreview);
            this.DetailedInfo_TextDownload = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextDownload);
            this.DetailedInfo_TextRating = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_Rating);

            this.DetailedInfo_TextVirtualPaperUid = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextVirtualPaperUid);
            this.DetailedInfo_TextRType = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextRType);
            this.DetailedInfo_TextAuthors = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextAuthors);
            this.DetailedInfo_TextDesc = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextDesc);
            this.DetailedInfo_TextPublishDate = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextPublishDate);
            this.DetailedInfo_TextTags = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextTags);
            this.DetailedInfo_TextResolution = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextResolution);
            this.DetailedInfo_TextAspectRadio = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextAspectRadio);
            this.DetailedInfo_TextFileExtension = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextFileExtension);
            this.DetailedInfo_TextFileSize = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextFileSize);
            this.DetailedInfo_TextVersionInfo = _localizer.GetLocalizedString(Constants.LocalText.DetailedInfo_TextVersionInfo);
        }

        public DetailedInfoViewModel(
            IWpMetadata data,
            bool editEnable,
            bool scoredEnable,
            bool downloadEnable) : this() {
            this._data = data;

            this.WallPaperUid = data.BasicData.WallpaperUid;
            this.ThumbnailPath = data.BasicData.ThumbnailPath;
            this.Rating = data.BasicData.Rating;
            this.RatingShow = Math.Max(0, this.Rating);
            this.Title = data.BasicData.Title;
            this.Desc = data.BasicData.Desc;
            //this.Type = data.BasicData.Type.ToString();
            this.Authors = string.Join(",", data.BasicData.Authors);
            this.PublishDate = data.BasicData.PublishDate;
            this.Tags = string.Join(",", data.BasicData.Tags);
            this.Resolution = data.BasicData.Resolution;
            this.AspectRatio = data.BasicData.AspectRatio;
            this.FileExtension = data.BasicData.FileExtension;
            this.FileSize = data.BasicData.FileSize;
            this.TextVersion = data.BasicData.AppInfo.AppVersion + "_" + data.BasicData.AppInfo.FileVersion;

            this.EditEnable = editEnable;
            this.BtnScoreVisibility = WallPaperUid.StartsWith("VP") && scoredEnable
                ? Visibility.Visible : Visibility.Collapsed;
            this.BtnDownloadVisibility = downloadEnable
            ? Visibility.Visible : Visibility.Collapsed;
        }

        internal async Task PreviewAsync() {
            await _wpControlClient.PreviewWallpaperAsync(_data);
        }

        //internal async Task SubmitSocreAsync(double rating)
        //{
        //    throw new NotImplementedException();
        //}

        private ILocalizer _localizer;
        private IWpMetadata _data;
        private IDialogService _dialogService;
        private IWallpaperControlClient _wpControlClient;
    }
}
