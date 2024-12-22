using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Models;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.WpSettingsComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScreenSaver : Page {
        public ScreenSaver() {
            this.InitializeComponent();

            _viewModel = App.Services.GetRequiredService<ScreenSaverViewModel>();
            this.DataContext = _viewModel;
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
    }
}
