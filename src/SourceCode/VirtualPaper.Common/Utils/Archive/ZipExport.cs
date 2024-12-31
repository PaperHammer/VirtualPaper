using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace VirtualPaper.Common.Utils.Archive {
    public static class ZipExport {

        /// <summary>
        /// Extract zip file to given output directory.
        /// </summary>
        /// <param name="archivePath">Source .zip path.</param>
        /// <param name="outFolder">Destination directory.</param>
        /// <param name="isVpFile">Verify whether the archive is vp format, throws Exception if not.</param>
        public static void ZipEportFile(string archivePath, string outFolder, bool isVpFile) {
            using (Stream fsInput = File.OpenRead(archivePath))
            using (var zf = new ZipFile(fsInput)) {

                if (isVpFile && zf.FindEntry("metaDate.json", true) == -1) {
                    throw new Exception("metaDate.json not found");
                }

                //long i = 0;
                foreach (ZipEntry zipEntry in zf) {
                    //progress
                    //float percentage = (float)++i / zf.Count;
                    //Debug.WriteLine(percentage + " " + zipEntry.PropertyName);

                    if (!zipEntry.IsFile) {
                        // Ignore directories
                        continue;
                    }

                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:
                    //entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here
                    // to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    // Manipulate the output filename here as desired.
                    var fullZipToPath = Path.Combine(outFolder, entryFileName);
                    var directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0) {
                        Directory.CreateDirectory(directoryName);
                    }

                    // 4K is optimum
                    var buffer = new byte[4096];

                    // Unzip file in buffered chunks. This is just as fast as unpacking
                    // to a buffer the full size of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (var zipStream = zf.GetInputStream(zipEntry))
                    using (Stream fsOutput = File.Create(fullZipToPath)) {
                        StreamUtils.Copy(zipStream, fsOutput, buffer);
                    }
                }
            }
        }

        /// <summary>
        /// Verify whether the archive is vp format.
        /// </summary>
        /// <param name="archivePath">Path to .zip file.</param>
        /// <returns></returns>
        public static bool IsVirtualPaperZip(string archivePath) {
            bool result = true;
            try {
                using (Stream fsInput = File.OpenRead(archivePath))
                using (var zf = new ZipFile(fsInput)) {

                    if (zf.FindEntry("metaDate.json", true) == -1) {
                        result = false;
                    }
                }
            }
            catch {
                result = false;
            }
            return result;
        }

        /// <summary>
        /// Extract zip files in filename order: 0.zip, 1.zip ...
        /// </summary>
        /// <param name="currentBundleVer">filename to start from</param>
        /// <param name="sourceDir">Source folder</param>
        /// <param name="destinationDir">Folder to extract</param>
        /// <returns>Last extracted filename</returns>
        public static int ExportAssetBundle(int currentBundleVer, string sourceDir, string destinationDir) {
            int maxExtracted = currentBundleVer;
            try {
                //wallpaper bundles filenames are 0.zip, 1.zip ...
                var sortedBundles = Directory.GetFiles(sourceDir).OrderBy(x => x);

                foreach (var item in sortedBundles) {
                    if (int.TryParse(Path.GetFileNameWithoutExtension(item), out int val)) {
                        if (val > maxExtracted) {
                            //will overwrite files if exists during extraction.
                            ZipEportFile(item, destinationDir, false);
                            maxExtracted = val;
                        }
                    }
                }
            }
            catch { /* TODO */ }
            return maxExtracted;
        }
    }
}
