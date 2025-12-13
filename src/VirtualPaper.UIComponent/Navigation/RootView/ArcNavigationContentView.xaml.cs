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
        public Dictionary<Type, ArcPage> PageBufferMap { get; } = [];
        public Frame ContentFrame => PART_ContentFrame;

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

        public void Navigate(Type pageType, NavigationPayload? parameter = null, ArcNavigationOptions? options = null) {
            this.ArcNavigate(PART_KeepAliveBuffer, pageType, parameter, options);
        }
    }
}
