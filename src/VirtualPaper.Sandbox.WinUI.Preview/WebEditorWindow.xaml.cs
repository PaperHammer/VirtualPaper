using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.Sandbox.WinUI.Preview {
    public sealed partial class WebEditorWindow : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => default;
        public override bool IsMainWindow => true;

        public WebEditorWindow(FrameworkPayload payload) {
            this.InitializeComponent();
            InitializeWindow();
            EditorPage.NavigateEnter(payload);
        }
    }
}
