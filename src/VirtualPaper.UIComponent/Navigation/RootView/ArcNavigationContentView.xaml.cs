using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Navigation {
    public sealed partial class ArcNavigationContentView : UserControl {
        public Dictionary<Type, ArcPage> PageMap { get; } = [];
        public Grid ContentGrid => PART_ContentGrid;

        public ObservableCollection<GlobalMsgInfo> InfobarMessages {
            get => (ObservableCollection<GlobalMsgInfo>)GetValue(InfobarMessagesProperty);
            set => SetValue(InfobarMessagesProperty, value);
        }
        public static readonly DependencyProperty InfobarMessagesProperty =
            DependencyProperty.Register(nameof(InfobarMessages),
                typeof(ObservableCollection<GlobalMsgInfo>),
                typeof(ArcNavigationContentView),
                new PropertyMetadata(null));

        public ArcNavigationContentView() {
            this.InitializeComponent();
        }

        public void Navigate(Type pageType, FrameworkPayload? parameter = null, ArcNavigationOptions? options = null) {
            this.ArcNavigate(pageType, parameter, options);
        }
    }
}
