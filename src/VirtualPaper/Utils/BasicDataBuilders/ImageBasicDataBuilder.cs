using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.Utils.BasicDataBuilders {
    /// <summary>
    /// 处理静态图（FImage）和动图（FGif）的元数据构建。
    /// </summary>
    internal class ImageBasicDataBuilder : IWpBasicDataBuilder {
        public void Build(string srcPath, string folderPath, string folderName, WpBasicData data, CancellationToken token) {
            // 复制源文件到库目录
            string destFilePath = Path.Combine(folderPath, folderName + Path.GetExtension(srcPath));
            if (srcPath != destFilePath) {
                File.Copy(srcPath, destFilePath, overwrite: true);
            }
            data.FilePath = destFilePath;

            // 生成缩略图 GIF
            string thumbnailPath = Path.Combine(folderPath, folderName + Constants.Field.ThumGifSuff);
            WallpaperUtil.CreateGif(srcPath, thumbnailPath, data.FType, token);
            data.ThumbnailPath = thumbnailPath;

            // 文件属性（分辨率、尺寸等）
            var prop = WallpaperUtil.GetWpProperty(srcPath, data.FType);
            data.Resolution = prop.Resolution;
            data.AspectRatio = prop.AspectRatio;
            data.FileSize = prop.FileSize;
            data.FileExtension = prop.FileExtension;
        }
    }
}
