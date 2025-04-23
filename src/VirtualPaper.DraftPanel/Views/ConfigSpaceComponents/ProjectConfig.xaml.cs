using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views.ConfigSpaceComponents {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ProjectConfig : Page {
        public ProjectConfig() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            _viewModel = ObjectProvider.GetRequiredService<ProjectConfigViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            _viewModel._configSpace = e.Parameter as ConfigSpace;
            _viewModel.InitContent();
            _viewModel.InitConfigSpace();
            this.DataContext = _viewModel;
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

        private void KeyboardAccelerator_Invoked_SearchTemplate(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            tbSearchName.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            args.Handled = true;
        }

        private void KeyboardAccelerator_Invoked_InputProjName(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            tbProjName.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            args.Handled = true;
        }

        private void KeyboardAccelerator_Invoked_Template(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) {
            FocusOnFirstItem();
            args.Handled = true;
        }

        private void FocusOnFirstItem() {
            if (templateListView.Items.Count > 0) {
                var firstItemContainer = templateListView.ContainerFromIndex(0) as ListViewItem;
                firstItemContainer?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private ProjectConfigViewModel _viewModel;
    }
}
