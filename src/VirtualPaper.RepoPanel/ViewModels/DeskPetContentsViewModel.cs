using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.Models.RepoPanel;
using VirtualPaper.Models.RepoPanel.Interfaces;
using VirtualPaper.RepoPanel.Utils;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.Storage;

namespace VirtualPaper.RepoPanel.ViewModels {
    public partial class DeskPetContentsViewModel : ObservableObject, IFilterable {
        public ObservableCollection<IDpBasicData> LibraryDeskPets { get; private set; } = null!;
        public LoadingStatus LibLoadingStatus { get; internal set; }

        private Brush _dpTitleForeground = new SolidColorBrush(Colors.White);
        public Brush DpTitleForeground {
            get { return _dpTitleForeground; }
            set { _dpTitleForeground = value; OnPropertyChanged(); }
        }

        public DeskPetContentsViewModel(
            IUserSettingsClient userSettingsClient,
            IDeskPetControlClient deskPetControlClient,
            RepoViewModel repoViewModel,
            DeskPetIndexService deskPetIndexService) {
            _userSettingsClient = userSettingsClient;
            _deskPetControlClient = deskPetControlClient;
            _repoViewModel = repoViewModel;
            _deskPetIndexService = deskPetIndexService;

            InitEvent();
            InitColletions();
            InitOthers();
        }

        private void InitOthers() {
            _deskPetIndexService.Initialize(_deskPetInstallFolders);
            _repoViewModel.RegisterChildContents(this);
        }

        private void InitEvent() {
            ArcThemeUtil.AppThemeChanged += (s, e) => {
                RefreshDpTitleForeground();
            };
        }

        internal void RefreshDpTitleForeground() {
            var color = ArcThemeUtil.GetFormatMainWindowTheme() == AppTheme.Light ? Colors.White : Colors.Black;
            DpTitleForeground = new SolidColorBrush(color);
        }

        private void InitColletions() {
            _deskPetInstallFolders = [
                _userSettingsClient.Settings.DeskPetDir,
            ];
            LibraryDeskPets = [];
            _libraryDeskPets = [];
        }

        internal async Task InitContentAsync() {
            if (_isInited) return;

            var ctx = ArcPageContextManager.GetContext<Repo>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        await _deskPetIndexService.Initialized.Task;

                        var entries = _deskPetIndexService.Query(_offset, _limit);
                        foreach (var entry in entries) {
                            var jsonPath = entry.JsonPath;
                            DpBasicData? data = await JsonSaver.LoadAsync<DpBasicData>(jsonPath, DpBasicDataContext.Default);
                            if (data == null || !data.IsAvailable())
                                continue;

                            LibraryDeskPets.Add(data);
                            _libraryDeskPets.Add(data);
                            _offset++;
                        }

                        _isInited = true;
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<DeskPetContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                });
        }

        internal async Task ApplyAsync(IDpBasicData data) {
            throw new NotImplementedException();
        }

        internal async Task DeleteAsync(IDpBasicData data) {
            throw new NotImplementedException();
        }

        internal async Task DropFilesAsync(IReadOnlyList<IStorageItem> items) {
            var ctx = ArcPageContextManager.GetContext<Repo>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsImport = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        List<ImportValue> importValues = await GetImportValueFromLocalAsync(items);
                        await ImportAsync(importValues);
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<DeskPetContentsViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                }, cts: ctsImport);
        }

        internal async Task PreviewAsync(IDpBasicData data) {
            throw new NotImplementedException();
        }

        internal void ShowDetail(IDpBasicData data) {
            throw new NotImplementedException();
        }

        internal void ShowEdit(IDpBasicData data) {
            throw new NotImplementedException();
        }

        internal async Task UpdateAsync(IDpBasicData data) {
            throw new NotImplementedException();
        }

        private async Task ImportAsync(List<ImportValue> importValues) {
            var ctx = ArcPageContextManager.GetContext<Repo>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            var ctsImport = new CancellationTokenSource();
            int finishedCnt = 0;
            int total = importValues.Count;
            await loadingCtx.RunWithProgressAsync(
                operation: async (token, reportProgress) => {
                    foreach (var importValue in importValues) {
                        try {
                            token.ThrowIfCancellationRequested();

                            if (importValue.Type != DeskPetEngineType.Unknown) {
                                var grpcData = await _deskPetControlClient.CreateBasicDataAsync(
                                    importValue.FilePath,
                                    importValue.Type,
                                    token);

                                if (grpcData == null) {
                                    GlobalMessageUtil.ShowError(
                                        nameof(Constants.I18n.InfobarMsg_ImportErr),
                                        isNeedLocalizer: true,
                                        extraMsg: importValue.FilePath);
                                    return;
                                }

                                var data = DataAssist.GrpcToDpBasicData(grpcData);
                                if (data.IsAvailable()) {
                                    UpdateLib(data);
                                }
                                else {
                                    GlobalMessageUtil.ShowError(
                                        nameof(Constants.I18n.InfobarMsg_ImportErr),
                                        isNeedLocalizer: true,
                                        extraMsg: importValue.FilePath);
                                }
                            }

                            reportProgress(++finishedCnt, total);
                        }
                        catch (Exception ex) when (
                            ex is OperationCanceledException ||
                            (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                            GlobalMessageUtil.ShowCanceled();
                            return;
                        }
                        catch (Exception ex) {
                            ArcLog.GetLogger<DeskPetContentsViewModel>().Error(ex);
                            GlobalMessageUtil.ShowException(ex);
                        }
                    }
                }, total: importValues.Count, cts: ctsImport);
        }

        private static async Task<List<ImportValue>> GetImportValueFromLocalAsync(IReadOnlyList<IStorageItem> items) {
            ConcurrentBag<ImportValue> importRes = [];
            SemaphoreSlim semaphore = new(20); // 并发度控制

            var tasks = items.Select(async item => {
                await semaphore.WaitAsync();

                try {
                    if (item is StorageFile file) {
                        importRes.Add(new(file.Path, FileFilter.GetDpFileType(file.Path)));
                    }
                    else if (item is StorageFolder folder) {
                        var subItems = await folder.GetItemsAsync();
                        var subResults = await GetImportValueFromLocalAsync(subItems);
                        foreach (var res in subResults) {
                            importRes.Add(res);
                        }
                    }
                }
                finally {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return [.. importRes];
        }

        private void UpdateLib(IDpBasicData data) {
            if (_deskPetIndexService.TryGetValue(data.Uid, out int idx)) {
                LibraryDeskPets[idx] = data;
                _libraryDeskPets[idx] = data;
            }
            else {
                LibraryDeskPets.Insert(0, data);
                _libraryDeskPets.Insert(0, data);
            }
            _deskPetIndexService.Update(data);
        }

        #region filter
        public FilterKey FilterKeyword { get; set; } = FilterKey.LibraryTitle;

        public void ApplyFilter(string keyword) {
            FilterByTitle(keyword);
        }

        internal void FilterByTitle(string keyword) {
            var filtered = _libraryDeskPets.Where(basicData =>
                basicData.Title != null && basicData.Title.Contains(keyword, StringComparison.InvariantCultureIgnoreCase)
            );
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private void Remove_NonMatching(IEnumerable<IDpBasicData> basicDatas) {
            for (int i = LibraryDeskPets.Count - 1; i >= 0; i--) {
                var item = LibraryDeskPets[i];
                if (!basicDatas.Contains(item)) {
                    LibraryDeskPets.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<IDpBasicData> basicDatas) {
            foreach (var item in basicDatas) {
                if (!LibraryDeskPets.Contains(item)) {
                    LibraryDeskPets.Add(item);
                }
            }
        }

        internal async void LoadMoreAsync() {
            try {
                LibLoadingStatus = LoadingStatus.Changing;
                await _deskPetIndexService.Initialized.Task;

                var entries = _deskPetIndexService.Query(_offset, _limit);
                foreach (var entry in entries) {
                    var jsonPath = entry.JsonPath;
                    DpBasicData? data = await JsonSaver.LoadAsync<DpBasicData>(jsonPath, DpBasicDataContext.Default);
                    if (data == null || !data.IsAvailable())
                        continue;

                    LibraryDeskPets.Add(data);
                    _libraryDeskPets.Add(data);
                    _offset++;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<DeskPetContentsViewModel>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
            finally {
                LibLoadingStatus = LoadingStatus.Ready;
            }
        }
        #endregion

        private struct ImportValue(string filePath, DeskPetEngineType type) {
            internal string FilePath { get; set; } = filePath;
            internal DeskPetEngineType Type { get; set; } = type;
        }

        private int _offset = 0;
        private readonly int _limit = 30;
        private readonly IDeskPetControlClient _deskPetControlClient;
        private readonly IUserSettingsClient _userSettingsClient;
        private readonly RepoViewModel _repoViewModel;
        private readonly DeskPetIndexService _deskPetIndexService;
        private List<string> _deskPetInstallFolders = [];
        private readonly Dictionary<string, ArcWindow> _details = [];
        private readonly Dictionary<string, ArcWindow> _edits = [];
        private readonly Dictionary<(string uid, DeskPetEngineType type), ArcWindow> _previews = [];
        private List<IDpBasicData> _libraryDeskPets = [];
        private bool _isInited;
    }
}
