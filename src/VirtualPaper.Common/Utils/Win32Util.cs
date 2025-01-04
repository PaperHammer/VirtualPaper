using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VirtualPaper.Common.Utils {
    public class Win32Util {
        /// <summary>
        /// 通过文件名称获取文件图标
        /// </summary>
        /// <param name="tcType">指定参数tcFullName的类型: FILE/DIR</param>
        /// <param name="tcFullName">需要获取图片的全路径文件名</param>
        /// <param name="tlIsLarge">是否获取大图标(32*32)</param>
        /// <returns></returns>
        public static Icon? GetIconByFileName(string tcType, string tcFullName, bool tlIsLarge = false) {
            string fileType = tcFullName.Contains('.') ? tcFullName.Substring(tcFullName.LastIndexOf('.')).ToLower() : string.Empty;
            string? regIconString = null;
            string systemDirectory = Environment.SystemDirectory + "\\";
            IntPtr[] phiconLarge = new IntPtr[1];
            IntPtr[] phiconSmall = new IntPtr[1];

            Icon? ico;
            nint hIcon;
            if (tcType == "FILE") {
                //含图标的文件，优先使用文件中自带图标
                if (".exe.ico".Contains(fileType)) {
                    //文件名 图标索引
                    phiconLarge[0] = phiconSmall[0] = IntPtr.Zero;
                    _ = ExtractIconEx(tcFullName, 0, phiconLarge, phiconSmall, 1);
                    hIcon = tlIsLarge ? phiconLarge[0] : phiconSmall[0];
                    ico = hIcon == IntPtr.Zero ? null : Icon.FromHandle(hIcon).Clone() as Icon;
                    if (phiconLarge[0] != IntPtr.Zero) _ = DestroyIcon(phiconLarge[0]);
                    if (phiconSmall[0] != IntPtr.Zero) _ = DestroyIcon(phiconSmall[0]);
                    if (ico != null) {
                        return ico;
                    }
                }

                //通过文件扩展名读取图标
                RegistryKey regVersion = Registry.ClassesRoot.OpenSubKey(fileType, false);
                if (regVersion != null) {
                    string regFileType = regVersion.GetValue("") as string;
                    regVersion.Close();
                    regVersion = Registry.ClassesRoot.OpenSubKey(regFileType + @"\DefaultIcon", false);
                    if (regVersion != null) {
                        regIconString = regVersion.GetValue("") as string;
                        regVersion.Close();
                    }
                }
                //没有读取到文件类型注册信息，指定为未知文件类型的图标
                regIconString ??= systemDirectory + "shell32.dll,0";
            }
            else {
                //直接指定为文件夹图标
                regIconString = systemDirectory + "shell32.dll,3";
            }

            string[] fileIcon = regIconString.Split([',']);
            //系统注册表中注册的标图不能直接提取，则返回可执行文件的通用图标
            fileIcon = fileIcon.Length == 2 ? fileIcon : [systemDirectory + "shell32.dll", "2"];

            phiconLarge[0] = phiconSmall[0] = IntPtr.Zero;
            _ = ExtractIconEx(fileIcon[0].Trim('\"'), Int32.Parse(fileIcon[1]), phiconLarge, phiconSmall, 1);
            hIcon = tlIsLarge ? phiconLarge[0] : phiconSmall[0];
            ico = hIcon == IntPtr.Zero ? null : Icon.FromHandle(hIcon).Clone() as Icon;
            if (phiconLarge[0] != IntPtr.Zero) _ = DestroyIcon(phiconLarge[0]);
            if (phiconSmall[0] != IntPtr.Zero) _ = DestroyIcon(phiconSmall[0]);
            if (ico != null) {
                return ico;
            }

            // 对于文件，如果提取文件图标失败，则重新使用可执行文件通用图标
            if (tcType == "FILE") {
                //系统注册表中注册的标图不能直接提取，则返回可执行文件的通用图标
                fileIcon = [systemDirectory + "shell32.dll", "2"];
                phiconLarge = new IntPtr[1];
                phiconSmall = new IntPtr[1];
                _ = ExtractIconEx(fileIcon[0], Int32.Parse(fileIcon[1]), phiconLarge, phiconSmall, 1);
                hIcon = tlIsLarge ? phiconLarge[0] : phiconSmall[0];
                ico = hIcon == IntPtr.Zero ? null : Icon.FromHandle(hIcon).Clone() as Icon;
                if (phiconLarge[0] != IntPtr.Zero) _ = DestroyIcon(phiconLarge[0]);
                if (phiconSmall[0] != IntPtr.Zero) _ = DestroyIcon(phiconSmall[0]);
            }

            return ico;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);
        [DllImport("User32.dll", SetLastError = true)]
        private static extern uint DestroyIcon(IntPtr phicon);
    }
}
