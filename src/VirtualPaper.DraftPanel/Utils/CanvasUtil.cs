using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.DraftPanel.Model.Runtime;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace VirtualPaper.DraftPanel.Utils {
    static class CanvasUtil {       
        internal static ImageSource RenderGridToBitmap(Grid grid) {
            // 确定输出图像的尺寸
            //int targetWidth = 100;
            //int targetHeight = 100;

            // 创建RenderTargetBitmap对象
            var renderTargetBitmap = new RenderTargetBitmap();
            //renderTargetBitmap.SetValue(RenderTargetBitmap.PixelWidthProperty, targetWidth);
            //renderTargetBitmap.SetValue(RenderTargetBitmap.PixelHeightProperty, targetHeight);

            // 渲染Grid到RenderTargetBitmap
            grid.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () => {
                await renderTargetBitmap.RenderAsync(grid);
            });

            return renderTargetBitmap;
        }
    }
}

/*
 * 直接从Canvas生成缩略图：
 * 这种方法类似于你之前提到的方法，通过使用RenderTargetBitmap来直接渲染整个Canvas到一个较小尺寸的图像。这种方式的优点是实现相对简单，因为不需要手动处理Canvas上的每个元素。
 * 它适用于当你希望快速获得整个Canvas的缩略图而不关心其中的具体细节。
 * 然而，这种方法可能不太适合需要对缩略图进行高度定制或优化的情况，因为它是“一锅端”的解决方案，不提供对单独元素的细粒度控制。
 * 
 * 先绘制内容再生成缩略图：
 * 如果你需要更多的控制或者想提高效率，可以考虑只绘制那些对缩略图有贡献的内容。这意味着你可能需要手动调整绘制逻辑，例如简化图形、减少颜色深度、忽略一些不必要的细节等，以达到提高性能的目的。
 * 这种方式的优点是可以根据缩略图的实际需求进行优化，从而可能生成更高效的缩略图。缺点是实现起来更为复杂，因为你需要为缩略图的生成专门编写或调整绘制代码。
 * 
 * 结论：
 * 如果目标是快速实现，并且对缩略图的质量和效率没有极端的要求，那么直接从Canvas生成缩略图（第一种方法）可能是更好的选择。
 * 如果追求的是更高的效率或需要对缩略图进行特别的优化，比如减少细节、降低分辨率等，则应该考虑第二种方法，即先绘制内容再生成缩略图。这通常需要更多的时间来开发和调试，但可以带来更好的性能和质量。
 */
