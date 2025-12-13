using System;
using System.Collections.Generic;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.PlayerWeb.Core.ViewModels {
    partial class GeneralInfoViewModel : ObservableObject {
        #region wallpaper data
        private string? _title;
        public string? Title {
            get => _title;
            private set {
                if (_title == value) return;
                _title = value;
                if (!string.IsNullOrEmpty(_title)) {
                    IsTitleVisible = true;
                    OnPropertyChanged();
                }
            }
        }

        private string? _desc;
        public string? Desc {
            get => _desc;
            private set {
                if (_desc == value) return;
                _desc = value;
                if (!string.IsNullOrEmpty(_desc)) {
                    IsDescVisible = true;
                    OnPropertyChanged();
                }
            }
        }

        private string? _authors;
        public string? Authors {
            get => _authors;
            private set {
                if (_authors == value) return;
                _authors = value;
                if (!string.IsNullOrEmpty(_authors)) {
                    AuthorList = [.. _authors.Split(';', StringSplitOptions.RemoveEmptyEntries)];
                    IsAuthorListVisible = true;
                }
            }
        }

        private string? _tags;
        public string? Tags {
            get => _authors;
            private set {
                if (_tags == value) return;
                _tags = value;
                if (!string.IsNullOrEmpty(_tags)) {
                    TagList = [.. _tags.Split(';', StringSplitOptions.RemoveEmptyEntries)];
                    IsTagListVisible = true;
                }
            }
        }

        private string? _publishDate;
        public string? PublishDate {
            get => _publishDate;
            private set {
                if (_publishDate == value) return;
                _publishDate = value;
                if (!string.IsNullOrEmpty(_publishDate)) {
                    IsPublishDateVisible = true;
                    OnPropertyChanged();
                }
            }
        }

        private List<string> _authorList = [];
        public List<string> AuthorList {
            get => _authorList;
            private set { _authorList = value; OnPropertyChanged(); }
        }

        private List<string> _tagList = [];
        public List<string> TagList {
            get => _tagList;
            private set { _tagList = value; OnPropertyChanged(); }
        }
        //public double Rating { get; set; } = -1;
        //public double RatingText { get; set; } = 0;

        private bool _isTitleVisible;
        public bool IsTitleVisible {
            get => _isTitleVisible;
            private set {
                if (_isTitleVisible != value) {
                    _isTitleVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isDescVisible;
        public bool IsDescVisible {
            get => _isDescVisible;
            private set {
                if (_isDescVisible != value) {
                    _isDescVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isAuthorListVisible;
        public bool IsAuthorListVisible {
            get => _isAuthorListVisible;
            private set {
                if (_isAuthorListVisible != value) {
                    _isAuthorListVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isTagListVisible;
        public bool IsTagListVisible {
            get => _isTagListVisible;
            private set {
                if (_isTagListVisible != value) {
                    _isTagListVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isPublishDateVisible;
        public bool IsPublishDateVisible {
            get => _isPublishDateVisible;
            private set {
                if (_isPublishDateVisible != value) {
                    _isPublishDateVisible = value;
                    OnPropertyChanged();
                }
            }
        }


        private string _resolution = string.Empty;
        public string Resolution {
            get => _resolution;
            private set {
                if (_resolution != value) {
                    _resolution = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _aspectRatio = string.Empty;
        public string AspectRatio {
            get => _aspectRatio;
            private set {
                if (_aspectRatio != value) {
                    _aspectRatio = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _fileExtension = string.Empty;
        public string FileExtension {
            get => _fileExtension;
            private set {
                if (_fileExtension != value) {
                    _fileExtension = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _fileSize = string.Empty;
        public string FileSize {
            get => _fileSize;
            private set {
                if (_fileSize != value) {
                    _fileSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _versionInfo = string.Empty;
        public string VersionInfo {
            get => _versionInfo;
            private set {
                if (_versionInfo != value) {
                    _versionInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        public GeneralInfoViewModel(string? wpBasicDataFilePath) {
            this._wpBasicDataFilePath = wpBasicDataFilePath;

            InitData();
        }

        private async void InitData() {
            if (_wpBasicDataFilePath == null) return;

            _wpBasicData ??= await JsonSaver.LoadAsync<WpBasicData>(_wpBasicDataFilePath, WpBasicDataContext.Default);
            Title = _wpBasicData.Title;
            Desc = _wpBasicData.Desc;
            Authors = _wpBasicData.Authors;
            Tags = _wpBasicData.Tags;
            //Rating = _wpBasicData.Rating;

            Resolution = _wpBasicData.Resolution;
            AspectRatio = _wpBasicData.AspectRatio;
            FileExtension = _wpBasicData.FileExtension;
            FileSize = _wpBasicData.FileSize;
            VersionInfo = $"APP {_wpBasicData.AppInfo.AppVersion} · Feature {_wpBasicData.AppInfo.FileVersion}";
        }

        private WpBasicData? _wpBasicData;
        private readonly string? _wpBasicDataFilePath;
    }
}
