using System.Threading.Tasks;

namespace VirtualPaper.UIComponent.Utils;

/// <summary>Called on the UI thread before starting an AI operation.</summary>
public interface IAiUseNoticeService {
    bool IsAcknowledged { get; }
    Task<bool> EnsureAcknowledgedAsync();
}
