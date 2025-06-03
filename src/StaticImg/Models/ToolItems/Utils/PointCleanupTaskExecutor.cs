using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.TaskManager;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils {
    internal class PointCleanupTaskExecutor : IBackgroundTaskExecutor {
        public PointCleanupTaskExecutor(ConcurrentBag<StrokePoint> pointsToCleanup, Action<Rect> onCleanup) {
            _pointsToCleanup = pointsToCleanup;
            _onCleanup = onCleanup;
        }

        public async Task ExecuteAsync(object parameters, CancellationToken cancellationToken) {
            if (parameters is not PointCleanupParameters cleanupParams) 
                throw new ArgumentException("Invalid parameters type");

            while (!cancellationToken.IsCancellationRequested) {
                try {
                    await Task.Delay(cleanupParams.CleanupInterval, cancellationToken);

                    var dirtyRect = CleanupExpiredPoints(cleanupParams.PointLifetime);
                    if (!dirtyRect.IsEmpty) {
                        _onCleanup?.Invoke(dirtyRect);
                    }
                }
                catch (TaskCanceledException) {
                    break;
                }
            }
        }

        private Rect CleanupExpiredPoints(TimeSpan lifetime) {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            var now = DateTime.UtcNow;

            var pointsToRemove = _pointsToCleanup
                .Where(p => p.IsDrawn && (now - p.CreationTime) > lifetime)
                .ToList();

            foreach (var point in pointsToRemove) {
                if (_pointsToCleanup.TryTake(out var removedPoint)) {
                    var bounds = removedPoint.GetBounds();
                    minX = Math.Min(minX, bounds.Left);
                    minY = Math.Min(minY, bounds.Top);
                    maxX = Math.Max(maxX, bounds.Right);
                    maxY = Math.Max(maxY, bounds.Bottom);
                }
            }

            return pointsToRemove.Count > 0
                ? new Rect(minX, minY, maxX - minX, maxY - minY)
                : Rect.Empty;
        }

        private readonly ConcurrentBag<StrokePoint> _pointsToCleanup;
        private readonly Action<Rect> _onCleanup;
    }

    public record PointCleanupParameters(TimeSpan PointLifetime, TimeSpan CleanupInterval);
}
