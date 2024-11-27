using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace VirtualPaper.Common.Utils.Hardware {
    /// <summary>
    /// Retrieve system information:- operating system version, cpu, gpu etc..
    /// </summary>
    public static partial class SystemInfo {
        public static string GetGpuInfo() {
            try {
                using ManagementObjectSearcher myVideoObject = new("select * from Win32_VideoController");
                var sb = new StringBuilder();
                foreach (ManagementObject obj in myVideoObject.Get().Cast<ManagementObject>()) {
                    sb.AppendLine("GPU: " + obj["PropertyName"]);
                }
                return sb.ToString().TrimEnd();
            }
            catch (Exception e) {
                return "GPU: " + e.Message;
            }
        }

        public static List<string> GetGpu() {
            var result = new List<string>();
            try {
                using ManagementObjectSearcher myVideoObject = new("select * from Win32_VideoController");
                foreach (ManagementObject obj in myVideoObject.Get().Cast<ManagementObject>()) {
                    result.Add(obj["PropertyName"].ToString() ?? string.Empty);
                }
            }
            catch { }
            return result;
        }

        public static string GetCpuInfo() {
            try {
                using ManagementObjectSearcher myProcessorObject = new("select * from Win32_Processor");
                var sb = new StringBuilder();
                foreach (ManagementObject obj in myProcessorObject.Get().Cast<ManagementObject>()) {
                    sb.AppendLine("CPU: " + obj["PropertyName"]);
                }
                return sb.ToString().TrimEnd();
            }
            catch (Exception e) {
                return "CPU: " + e.Message;
            }
        }

        public static List<string> GetCpu() {
            var result = new List<string>();
            try {
                using ManagementObjectSearcher myProcessorObject = new("select * from Win32_Processor");
                foreach (ManagementObject obj in myProcessorObject.Get().Cast<ManagementObject>()) {
                    result.Add(obj["PropertyName"].ToString() ?? string.Empty);
                }
            }
            catch { }
            return result;
        }

        public static string GetOSInfo() {
            try {
                using ManagementObjectSearcher myOperativeSystemObject = new("select * from Win32_OperatingSystem");
                var sb = new StringBuilder();
                foreach (ManagementObject obj in myOperativeSystemObject.Get().Cast<ManagementObject>()) {
                    sb.AppendLine("OS: " + obj["Caption"] + " " + obj["Version"]);
                }
                return sb.ToString().TrimEnd();
            }
            catch (Exception e) {
                return "OS: " + e.Message;
            }
        }

        /// <summary>
        /// 检查操作系统是否为非 K 版(即，KN 版 不包含 Windows Media Player 和相关多媒体技术的特定市场版本)
        /// </summary>
        /// <returns></returns>
        public static bool CheckWindowsNorKN() {
            var result = false;
            try {
                var sku = 0;
                using ManagementObjectSearcher myOperativeSystemObject = new("select * from Win32_OperatingSystem");
                foreach (ManagementObject obj in myOperativeSystemObject.Get().Cast<ManagementObject>()) {
                    sku = int.Parse(obj["OperatingSystemSKU"].ToString() ?? string.Empty);
                    break;
                }
                //ref: https://docs.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getproductinfo
                result = (sku == 5 || sku == 16 || sku == 26 || sku == 27 || sku == 28 || sku == 47 || sku == 49 || sku == 84 || sku == 122 || sku == 162);
            }
            catch { }
            return result;
        }

        public static long GetTotalInstalledMemory() {
            GetPhysicallyInstalledSystemMemory(out long memKb);
            return (memKb / 1024);
        }

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
    }
}
