using System.Runtime.InteropServices;

namespace VirtualPaper.Common.Utils.PInvoke {
    public static partial class NativeWindowsBuild {
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEX lpVersionInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct RTL_OSVERSIONINFOEX {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
        }

        public static uint GetBuildNumber() {
            var info = new RTL_OSVERSIONINFOEX {
                dwOSVersionInfoSize = (uint)Marshal.SizeOf<RTL_OSVERSIONINFOEX>()
            };

            RtlGetVersion(ref info);
            return info.dwBuildNumber;
        }
        
        public static WindowsBuild GetBuildEnum() {
            return MapBuildToFeature(GetBuildNumber());
        }

        public static WindowsBuild MapBuildToFeature(uint build) {
            return build switch {
                >= 26200 => WindowsBuild.Win11_25H2,
                >= 26100 => WindowsBuild.Win11_24H2,
                >= 22631 => WindowsBuild.Win11_23H2,
                >= 22621 => WindowsBuild.Win11_22H2,
                >= 22000 => WindowsBuild.Win11_21H2,

                >= 19045 => WindowsBuild.Win10_22H2,
                >= 19044 => WindowsBuild.Win10_21H2,
                >= 19043 => WindowsBuild.Win10_21H1,
                >= 19042 => WindowsBuild.Win10_20H2,

                _ => WindowsBuild.Unknown
            };
        }
    }

    public enum WindowsBuild {
        Unknown,
        Win10_20H2,
        Win10_21H1,
        Win10_21H2,
        Win10_22H2,
        Win11_21H2,
        Win11_22H2,
        Win11_23H2,
        Win11_24H2,
        Win11_25H2,
    }
}
