using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Navigation {
    public sealed partial class ArcNavigationContentView : UserControl {
        public Dictionary<Type, ArcPage> PageBufferMap { get; } = [];
        public Frame ContentFrame => PART_ContentFrame;

        public ArcNavigationContentView() {
            this.InitializeComponent();
        }

        public void Navigate(Type pageType, object? parameter = null, ArcNavigationOptions? options = null) {
            this.ArcNavigate(PART_KeepAliveBuffer, pageType, parameter, options);
        }
    }
}
