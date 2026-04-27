using Grpc.Core;
using VirtualPaper.Grpc.Service.StyleTransfer;

namespace VirtualPaper.GrpcServers {
    public class StyleTransferServer : Grpc_StyleTransferService.Grpc_StyleTransferServiceBase {
        public override Task<CapabilityResponse> GetCapability(Empty request, ServerCallContext context) {
            return base.GetCapability(request, context);
        }

        public override Task StylizeWithProgress(StyleRequest request, IServerStreamWriter<ProgressEvent> responseStream, ServerCallContext context) {
            return base.StylizeWithProgress(request, responseStream, context);
        }
    }
}
