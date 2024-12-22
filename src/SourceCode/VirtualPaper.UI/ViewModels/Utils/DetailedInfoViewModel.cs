using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Services.Interfaces;
using VirtualPaper.UI.Utils;
using VirtualPaper.UIComponent.Utils;
using WinUI3Localizer;
using static Vanara.PInvoke.User32.RAWINPUT;

namespace VirtualPaper.UI.ViewModels.Utils {
    public partial class DetailedInfoViewModel : ObservableObject {
        #region wallpaper data
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
        #endregion

        #region text
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
        #endregion

        #region loading
        private bool _frameIsEnable = false;
        public bool FrameIsEnable {
            get { return _frameIsEnable; }
            set { _frameIsEnable = value; OnPropertyChanged(); }
        }

        private bool _loadingIsVisiable = true;
        public bool LoadingIsVisiable {
            get { return _loadingIsVisiable; }
            set { _loadingIsVisiable = value; OnPropertyChanged(); }
        }

        private int _curValue;
        public int CurValue {
            get { return _curValue; }
            set { _curValue = value; OnPropertyChanged(); }
        }

        private int _totalValue;
        public int TotalValue {
            get { return _totalValue; }
            set { _totalValue = value; OnPropertyChanged(); }
        }

        private bool _cancelEanble;
        public bool CancelEnable {
            get { return _cancelEanble; }
            set { _cancelEanble = value; OnPropertyChanged(); }
        }

        private bool _progressbarEnable;
        public bool ProgressbarEnable {
            get { return _progressbarEnable; }
            set { _progressbarEnable = value; OnPropertyChanged(); }
        }

        private string _textLoading;
        public string TextLoading {
            get { return _textLoading; }
            set { _textLoading = value; OnPropertyChanged(); }
        }

        private string _textCancel;
        public string TextCancel {
            get { return _textCancel; }
            set { _textCancel = value; OnPropertyChanged(); }
        }

        private CancellationTokenSource[] _ctsTokens;
        public CancellationTokenSource[] CtsTokens {
            get { return _ctsTokens; }
            set { _ctsTokens = value; OnPropertyChanged(); }
        }
        #endregion

        public DetailedInfoViewModel() {
            this._dialogService = App.Services.GetRequiredService<IDialogService>();
            this._wpControlClient = App.Services.GetRequiredService<IWallpaperControlClient>();

            _localizer = LanguageUtil.LocalizerInstacne;

            InitText();
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

        private void InitText() {
            DetailedInfo_TextScore = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextScore);
            DetailedInfo_TextScoreTilte = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextScoreTilte);
            DetailedInfo_TextScoreSubmit = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextScoreSubmit);
            DetailedInfo_TextPreview = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextPreview);
            DetailedInfo_TextDownload = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextDownload);
            DetailedInfo_TextRating = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_Rating);

            DetailedInfo_TextVirtualPaperUid = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextVirtualPaperUid);
            DetailedInfo_TextRType = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextRType);
            DetailedInfo_TextAuthors = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextAuthors);
            DetailedInfo_TextDesc = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextDesc);
            DetailedInfo_TextPublishDate = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextPublishDate);
            DetailedInfo_TextTags = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextTags);
            DetailedInfo_TextResolution = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextResolution);
            DetailedInfo_TextAspectRadio = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextAspectRadio);
            DetailedInfo_TextFileExtension = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextFileExtension);
            DetailedInfo_TextFileSize = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextFileSize);
            DetailedInfo_TextVersionInfo = _localizer.GetLocalizedString(Constants.I18n.DetailedInfo_TextVersionInfo);

            TextLoading = _localizer.GetLocalizedString(Constants.I18n.Text_Loading);
            TextCancel = _localizer.GetLocalizedString(Constants.I18n.Text_Cancel);
        }

        internal async Task PreviewAsync() {
            //try {
            //    Loading(true, false, [_ctsPreview]);
            //    _ctsPreview = new CancellationTokenSource();
            //    await _wpControlClient.PreviewWallpaperAsync(_data, _ctsPreview.Token);
            //}
            //finally {
            //    Loaded([_ctsPreview]);
            //}
            //try {
            //    _ctsPreview = new CancellationTokenSource();
            //    Loading(true, false, [_ctsPreview]);

            //    bool isAvailable = await PreRun(_data, _ctsPreview);
            //    if (!isAvailable) return;

            //    bool isStarted = await _wpControlClient.PreviewWallpaperAsync(_data, _ctsPreview.Token);
            //    if (!isStarted) {
            //        throw new Exception("Preview Failed.");
            //    }
            //}
            //catch (OperationCanceledException) {
            //    ShowCanceled();
            //}
            //catch (Exception ex) {
            //    ShowExp(ex);
            //}
            //finally {
            //    Loaded([_ctsPreview]);
            //}
        }

        //internal async Task SubmitSocreAsync(double rating)
        //{
        //    throw new NotImplementedException();
        //}

        #region loading_ui_logic
        internal async void Loading(
            bool cancelEnable,
            bool progressbarEnable,
            CancellationTokenSource[] cts) {

            if (_loadingSemaphoreSlim.CurrentCount == 0) return;

            await _loadingSemaphoreSlim.WaitAsync();

            FrameIsEnable = false;
            LoadingIsVisiable = true;
            CtsTokens = cts;
            CancelEnable = cancelEnable;
            ProgressbarEnable = progressbarEnable;
        }

        internal void Loaded(CancellationTokenSource[] cts) {
            if (cts != null) {
                foreach (var ct in cts) {
                    ct?.Dispose();
                }
            }

            LoadingIsVisiable = false;
            FrameIsEnable = true;

            if (_loadingSemaphoreSlim.CurrentCount < 1) {
                _loadingSemaphoreSlim.Release();
            }
        }

        internal void UpdateProgressbarValue(int curValue, int toltalValue) {
            TotalValue = toltalValue;
            CurValue = curValue;
        }
        #endregion

        private readonly ILocalizer _localizer;
        private readonly IWpMetadata _data;
        private readonly IDialogService _dialogService;
        private readonly IWallpaperControlClient _wpControlClient;
        private readonly SemaphoreSlim _loadingSemaphoreSlim = new(1, 1);
    }
}
