namespace VirtualPaper.Common.Utils.Files.Models {
    public class FileData(FileType ftype, string[] extensions) {
        public FileType FType { get; set; } = ftype;
        public string[] Extentions { get; set; } = extensions;
    }
}
