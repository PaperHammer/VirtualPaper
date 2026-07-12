using System;
using Microsoft.UI.Xaml;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop;

namespace VirtualPaper.Sandbox.WinUI.Preview {
    public sealed partial class WebEditorWindow : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => default;
        protected override bool IsNeedTrack => false;

        public WebEditorWindow(FrameworkPayload payload) {
            this.InitializeComponent();
            InitializeWindow();
            EditorPage.NavigateEnter(payload);
        }
    }
}
