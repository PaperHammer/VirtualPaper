namespace VirtualPaper.Models.Cores
{
    public class ApplicationInfo
    {
        public string AppName { get; init; } = string.Empty;
        public string AppVersion { get; init; } = string.Empty;

        public ApplicationInfo()
        {
            AppName = "VirtualPaper";
            AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
    }
}
