using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models;
using VirtualPaper.WpSettingsPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.WpSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScreenSaver : Page, IDisposable {
        public ScreenSaver() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._wpSettingsPanel == null) {
                this._wpSettingsPanel = e.Parameter as IWpSettingsPanel;

                _viewModel = ObjectProvider.GetRequiredService<ScreenSaverViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
                _viewModel._wpSettingsPanel = this._wpSettingsPanel;
                this.DataContext = _viewModel;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            base.OnNavigatingFrom(e);

            _viewModel.StopListenForClients();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            _ = _viewModel.ListenForClients();
        }

        private void IsRunningLock_Checked(object sender, RoutedEventArgs e) {
            _viewModel.IsRunningLock = true;
        }

        private void IsRunningLock_Unchecked(object sender, RoutedEventArgs e) {
            _viewModel.IsRunningLock = false;
        }

        private void RightClickMenuItem_Click(object sender, RoutedEventArgs e) {
            var item = (sender as FrameworkElement).DataContext;
            var procInfo = item as ProcInfo;
            _viewModel.RemoveFromWhiteScr(procInfo);
        }

        private void AddToWhiteListBtn_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new() {
                Filter = "Executable Files (*.exe)|*.exe"
            };
            bool? result = openFileDialog.ShowDialog();

            if (result == true) {
                string procPath = openFileDialog.FileName;
                string procName = Path.GetFileNameWithoutExtension(procPath);

                using System.Drawing.Image img = Win32Util.GetIconByFileName("FILE", procPath).ToBitmap();
                string iconPath = Path.Combine(Constants.CommonPaths.ExeIconDir, procName) + ".png";
                img.Save(iconPath);

                _viewModel.AddToWhiteListScr(new ProcInfo(procName, procPath, iconPath));
            }
        }

        // Whenever text changes in any of the filtering text boxes, the following function is called:
        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            // This is a Linq query that selects only items that return True after being passed through
            // the Filter function, and adds all of those selected items to filtered.
            var filtered = _viewModel._whiteListScr.Where(Filter);
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        /* When the text in any filter is changed, perform a check on each item in the original
        proc list to see if the item should be displayed, taking into account all three of the
        filters currently applied. If the item passes all three checks for all three filters,
        the function returns true and the item is added to the filtered list above. */
        private bool Filter(ProcInfo proc) {
            return proc.ProcName.Contains(TargetProName.Text, StringComparison.InvariantCultureIgnoreCase);
        }

        /* These functions go through the current list being displayed (procsFiltered), and remove
        any items not in the filtered collection (any items that don't belong), or add back any items
        from the original allProcs list that are now supposed to be displayed (i.e. when backspace is hit). */
        private void Remove_NonMatching(IEnumerable<ProcInfo> procInfos) {
            for (int i = _viewModel.ProcsFiltered.Count - 1; i >= 0; i--) {
                var item = _viewModel.ProcsFiltered[i];
                // If proc is not in the filtered argument list, remove it from the ListView's source.
                if (!procInfos.Contains(item)) {
                    _viewModel.ProcsFiltered.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<ProcInfo> procInfos) {
            foreach (var item in procInfos) {
                // If item in filtered list is not currently in ListView's source collection, add it back in
                if (!_viewModel.ProcsFiltered.Contains(item)) {
                    _viewModel.ProcsFiltered.Add(item);
                }
            }
        }

        private ScreenSaverViewModel _viewModel;
        private IWpSettingsPanel _wpSettingsPanel;
        private bool disposedValue;

        private void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: 释放托管状态(托管对象)
                }


                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
