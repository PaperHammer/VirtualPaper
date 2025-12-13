using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;

// To learn more about WinUI, the WinUI draft structure,
// and more about our draft templates, see: http://aka.ms/winui-draft-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    public sealed partial class DraftConfig : Page {
        public DraftConfig() {
            _viewModel = ObjectProvider.GetRequiredService<DraftConfigViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            this.DataContext = _viewModel;
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            _viewModel._configSpace = e.Parameter as ConfigSpace;
            _viewModel.InitContent();
            _viewModel.InitConfigSpace();
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e) {
            var filtered = _viewModel._availableTemplates.Where(Filter);
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private bool Filter(ProjectTemplate template) {
            return template.Name.Contains(tbSearchName.Text, StringComparison.InvariantCultureIgnoreCase);
        }

        private void Remove_NonMatching(IEnumerable<ProjectTemplate> templates) {
            for (int i = _viewModel.AvailableTemplates.Count - 1; i >= 0; i--) {
                var item = _viewModel.AvailableTemplates[i];
                if (!templates.Contains(item)) {
                    _viewModel.AvailableTemplates.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<ProjectTemplate> templates) {
            foreach (var item in templates) {
                if (!_viewModel.AvailableTemplates.Contains(item)) {
                    _viewModel.AvailableTemplates.Add(item);
                }
            }
        }

        //private void KeyboardAccelerator_Invoked_SearchTemplate(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
        //    tbSearchName.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        //    args.Handled = true;
        //}

        //private void KeyboardAccelerator_Invoked_InputProjName(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
        //    tbProjName.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        //    args.Handled = true;
        //}

        //private void KeyboardAccelerator_Invoked_Template(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
        //    FocusOnFirstItem();
        //    args.Handled = true;
        //}

        private void FocusOnFirstItem() {
            if (templateListView.Items.Count > 0) {
                var firstItemContainer = templateListView.ContainerFromIndex(0) as ListViewItem;
                firstItemContainer?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private DraftConfigViewModel _viewModel;
    }
}
