using System.Threading.Tasks;

namespace Workloads.Creation.StaticImg.Models.Specific.Strategies {
    public interface IInkFileStrategy {
        Task<bool> LoadAsync(InkCanvasData data);
        Task<(bool Success, string? FinalPath)> SaveAsync(InkCanvasData data);
        Task<(bool Success, string? FinalPath)> SaveAtEmergencyAsync(InkCanvasData data);
    }
}
