using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.AccountPanel.ViewModels {
    partial class UploadViewModel : ObservableObject {
        private IWpBasicData _wpBasicData;
        public IWpBasicData WpBasicData {
            get { return _wpBasicData; }
            set {
                _wpBasicData = value;
                IsEnable = value != null;
                OnPropertyChanged();
            }
        }

        private bool _isEnable;
        public bool IsEnable {
            get { return _isEnable; }
            set { _isEnable = value; OnPropertyChanged(); }
        }

        private string _title;
        public string Title {
            get { return _title; }
            set { if (_title == value) return; IsOk = value?.Length > 0; _title = value; OnPropertyChanged(); }
        }

        private string _desc;
        public string Desc {
            get { return _desc; }
            set { if (_desc == value) return;  _desc = value; OnPropertyChanged(); }
        }

        private string _selectedPartition;
        public string SelectedPartition {
            get { return _selectedPartition; }
            set { if (_selectedPartition == value) return; _selectedPartition = value; OnPropertyChanged(); }
        }

        private bool _isOk;
        public bool IsOk {
            get { return _isOk; }
            set { _isOk = value; OnPropertyChanged(); }
        }

        public List<string> Partitions { get; private set; } = [];
        public ObservableCollection<string> TagList { get; set; } = [];

        public UploadViewModel(
            IAccountClient accountClient,
            IWallpaperControlClient wpControlClient) {
            _accountClient = accountClient;
            _wpControlClient = wpControlClient;
        }

        internal async Task<bool> InitPartitionsAsync() {
            try {
                Account.Instance.GetNotify().Loading(false, false);
                Partitions.Clear();

                var response = await _accountClient.GetPartitionsAsync();
                if (!response.Success) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                    return false;
                }
                foreach (var part in response.Partitions) {
                    Partitions.Add(part);
                }

                return true;
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Account.Instance.GetNotify().Loaded();
            }

            return false;
        }

        internal async Task TryImportAsync(string filePath) {
            try {
                _ctsImport = new CancellationTokenSource();
                Account.Instance.GetNotify().Loading(true, true, [_ctsImport]);
                var ftype = FileFilter.GetFileType(filePath);

                if (ftype != FileType.FUnknown) {
                    var grpc_data = await _wpControlClient.CreateBasicDataInMemAsync(
                        filePath,
                        ftype,
                        _ctsImport.Token);
                    WpBasicData data = DataAssist.GrpcToBasicData(grpc_data);
                    if (data.IsAvailable()) {
                        WpBasicData = data;
                    }
                    else {
                        Account.Instance.GetNotify().ShowMsg(
                            false,
                            Constants.I18n.InfobarMsg_ImportErr,
                            InfoBarType.Error,
                            filePath);
                    }
                }
                else {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        Constants.I18n.Dialog_Content_Import_Failed_Lib,
                       InfoBarType.Error,
                       filePath);
                }
            }
            catch (RpcException ex) {
                if (ex.StatusCode == StatusCode.Cancelled) {
                    Account.Instance.GetNotify().ShowCanceled();
                }
                else {
                    Account.Instance.GetNotify().ShowExp(ex);
                }
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Account.Instance.GetNotify().Loaded([_ctsImport]);
            }
        }

        internal async Task UploadWallpaperAsync() {
            try {
                _ctsUplaod = new CancellationTokenSource();
                Account.Instance.GetNotify().Loading(true, true, [_ctsUplaod]);

                MergeInput();
                var response = await _accountClient.UploadWallpaperAsync(WpBasicData, _ctsUplaod.Token);
                if (!response.Success) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                    return;
                }
                Account.Instance.GetNotify().ShowMsg(
                    true,
                    response.Message,
                    InfoBarType.Success,
                    key: response.Message,
                    isAllowDuplication: false);

                Clear();
            }
            catch (RpcException ex) {
                if (ex.StatusCode == StatusCode.Cancelled) {
                    Account.Instance.GetNotify().ShowCanceled();
                }
                else {
                    Account.Instance.GetNotify().ShowExp(ex);
                }
            }
            catch (Exception ex) {
                Account.Instance.GetNotify().ShowExp(ex);
            }
            finally {
                Account.Instance.GetNotify().Loaded([_ctsUplaod]);
            }
        }

        private void MergeInput() {
            WpBasicData.Title = Title;
            WpBasicData.Desc = Desc;
            WpBasicData.Partition = SelectedPartition ?? Partitions.FirstOrDefault(string.Empty);
            WpBasicData.Tags = string.Join(';', TagList);
            WpBasicData.PublishDate = DateTime.Now.ToString();
        }

        internal void Clear() {
            this.WpBasicData = null;
            this.TagList.Clear();
            Title = null;
            Desc = null;
            SelectedPartition = null;
        }

        internal void FillData() {
            Title = WpBasicData.Title;
            Desc = WpBasicData.Desc;
            TagList = [.. WpBasicData.Tags.Split(';')];
            SelectedPartition = WpBasicData.Partition;
        }

        private readonly IAccountClient _accountClient;
        private readonly IWallpaperControlClient _wpControlClient;
        private CancellationTokenSource _ctsImport, _ctsUplaod;
    }
}
