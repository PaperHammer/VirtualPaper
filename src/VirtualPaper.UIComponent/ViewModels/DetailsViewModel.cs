using System;
using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.ViewModels {
    class DetailsViewModel {
        #region wallpaper data
        private string _title;
        public string Title {
            get => _title;
            set {
                _title = value;
                if (!string.IsNullOrEmpty(_title)) {
                    IsTitleVisible = true;
                }
            }
        }

        private string _desc;
        public string Desc {
            get => _desc;
            set {
                _desc = value;
                if (!string.IsNullOrEmpty(_desc)) {
                    IsDescVisible = true;
                }
            }
        }

        private string _authors;
        public string Authors {
            get => _authors;
            set {
                _authors = value;
                if (!string.IsNullOrEmpty(_authors)) {
                    AuthorList = [.. _authors.Split(';', StringSplitOptions.RemoveEmptyEntries)];
                    IsAuthorListVisible = true;
                }
            }
        }

        private string _tags;
        public string Tags {
            get => _authors;
            set {
                _tags = value;
                if (!string.IsNullOrEmpty(_tags)) {
                    TagList = [.. _tags.Split(';', StringSplitOptions.RemoveEmptyEntries)];
                    IsTagListVisible = true;
                }
            }
        }

        private string _publishDate;
        public string PublishDate {
            get => _publishDate;
            set {
                _publishDate = value;
                if (!string.IsNullOrEmpty(_publishDate)) {
                    IsPublishDateVisible = true;
                }
            }
        }

        public List<string> AuthorList { get; set; } = [];
        public List<string> TagList { get; set; } = [];
        //public double Rating { get; set; } = -1;
        //public double RatingText { get; set; } = 0;

        public bool IsTitleVisible { get; set; }
        public bool IsDescVisible { get; set; }
        public bool IsAuthorListVisible { get; set; }
        public bool IsTagListVisible { get; set; }
        public bool IsPublishDateVisible { get; set; }

        public string Resolution { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string VersionInfo { get; set; } = string.Empty;
        #endregion

        #region text
        public string Details_TextResolution { get; set; } = string.Empty;
        public string Details_TextAspectRadio { get; set; } = string.Empty;
        public string Details_TextFileExtension { get; set; } = string.Empty;
        public string Details_TextFileSize { get; set; } = string.Empty;
        public string Details_TextVersionInfo { get; set; } = string.Empty;
        #endregion

        public DetailsViewModel() {
            InitText();
        }

        public DetailsViewModel(string wpBasicDataFilePath) : this() {
            _wpBasicData = JsonSaver.Load<WpBasicData>(wpBasicDataFilePath, WpBasicDataContext.Default);
            InitData();
        }

        public DetailsViewModel(IWpBasicData wpBasicData) : this() {
            _wpBasicData = wpBasicData;
            InitData();
        }

        private void InitText() {
            Details_TextResolution = LanguageUtil.GetI18n(Constants.I18n.Text_Resolution);
            Details_TextAspectRadio = LanguageUtil.GetI18n(Constants.I18n.Text_AspectRatio);
            Details_TextFileExtension = LanguageUtil.GetI18n(Constants.I18n.Text_FileExtension);
            Details_TextFileSize = LanguageUtil.GetI18n(Constants.I18n.Text_FileSize);
            Details_TextVersionInfo = LanguageUtil.GetI18n(Constants.I18n.Text_VersionInfo);
        }

        private void InitData() {
            Title = _wpBasicData.Title;
            Desc = _wpBasicData.Desc;
            Authors = _wpBasicData.Authors;
            Tags = _wpBasicData.Tags;
            //Rating = _wpBasicData.Rating;

            Resolution = _wpBasicData.Resolution;
            AspectRatio = _wpBasicData.AspectRatio;
            FileExtension = _wpBasicData.FileExtension;
            FileSize = _wpBasicData.FileSize;
            VersionInfo = $"{_wpBasicData.AppInfo.AppVersion}_{_wpBasicData.AppInfo.FileVersion}";
        }

        private readonly IWpBasicData _wpBasicData;
    }
}
