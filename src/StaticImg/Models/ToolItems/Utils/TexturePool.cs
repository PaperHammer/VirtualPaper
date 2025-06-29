//using System.Collections.Concurrent;
//using System.Threading.Tasks;
//using Microsoft.Graphics.Canvas;
//using Windows.Foundation;

//namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils {
//    // 纹理对象池（减少GPU资源分配开销）
//    public static class TexturePool {
//        private static readonly ConcurrentBag<CanvasRenderTarget> _pool = [];
//        private static readonly ConcurrentDictionary<Size, ConcurrentBag<CanvasRenderTarget>> _sizePools = new();

//        public static CanvasRenderTarget Acquire(Size size) {
//            if (_sizePools.TryGetValue(size, out var pool) && pool.TryTake(out var texture))
//                return texture;

//            return new CanvasRenderTarget(
//                MainPage.Instance.SharedDevice,
//                (float)size.Width,
//                (float)size.Height,
//                96f);
//        }

//        public static void Release(CanvasRenderTarget texture) {
//            var size = new Size(texture.Size.Width, texture.Size.Height);
//            var pool = _sizePools.GetOrAdd(size, _ => []);
//            pool.Add(texture);
//        }

//        // 预热内存池（启动时调用）
//        public static void WarmUp(int[] commonSizes) {
//            Parallel.ForEach(commonSizes, size => {
//                var pool = _sizePools.GetOrAdd(new Size(size, size), _ => new ConcurrentBag<CanvasRenderTarget>());
//                for (int i = 0; i < 5; i++) {
//                    pool.Add(new CanvasRenderTarget(
//                        MainPage.Instance.SharedDevice,
//                        size, size, 96f));
//                }
//            });
//        }
//    }
//}
