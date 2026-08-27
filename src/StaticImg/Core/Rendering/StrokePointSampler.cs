using System.Collections.Generic;
using System.Numerics;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    /// <summary>
    /// 对连续指针采样做轻量在线简化，过滤亚像素抖动和近似共线的冗余点。
    /// </summary>
    internal static class StrokePointSampler {
        internal static bool AddOrUpdate(IList<Vector2> points, Vector2 sample) {
            if (!float.IsFinite(sample.X) || !float.IsFinite(sample.Y)) return false;

            if (points.Count == 0) {
                points.Add(sample);
                return true;
            }

            Vector2 last = points[^1];
            if (Vector2.DistanceSquared(last, sample) < MinimumDistanceSquared)
                return false;

            if (points.Count >= 2 && CanReplaceLastPoint(points[^2], last, sample)) {
                points[^1] = sample;
                return true;
            }

            points.Add(sample);
            return true;
        }

        private static bool CanReplaceLastPoint(Vector2 anchor, Vector2 last, Vector2 sample) {
            Vector2 previousDirection = last - anchor;
            Vector2 nextDirection = sample - last;

            // 回头或形成明显转角时必须保留 last，否则尖角会被拉成直线。
            if (Vector2.Dot(previousDirection, nextDirection) < 0) return false;

            Vector2 chord = sample - anchor;
            float chordLengthSquared = chord.LengthSquared();
            if (chordLengthSquared <= float.Epsilon) return false;

            float projection = Vector2.Dot(last - anchor, chord) / chordLengthSquared;
            if (projection <= 0 || projection >= 1) return false;

            Vector2 projectedPoint = anchor + (chord * projection);
            return Vector2.DistanceSquared(last, projectedPoint) <= CollinearToleranceSquared;
        }

        // 阈值使用画布像素单位。共线容差必须足够小，避免缓慢曲线的控制点被周期性替换。
        private const float MinimumDistanceSquared = 0.25f;
        private const float CollinearToleranceSquared = 0.01f;
    }
}
