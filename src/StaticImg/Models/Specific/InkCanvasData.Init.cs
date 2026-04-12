using VirtualPaper.UIComponent.Context;
using Workloads.Creation.StaticImg.Core.Utils;

namespace Workloads.Creation.StaticImg.Models.Specific {
    // Init ctor part of InkCanvasData
    public partial class InkCanvasData {
        public InkProjectSession Session => _session;
        public ArcPageContext Context => _context;

        public InkCanvasData(InkProjectSession session, ArcPageContext context) {
            _session = session;
            _context = context;
        }

        private readonly InkProjectSession _session;
        private readonly ArcPageContext _context;
    }
}
