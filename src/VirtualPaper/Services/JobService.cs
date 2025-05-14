using System.Diagnostics;
using System.Runtime.InteropServices;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    internal class JobService : IJobService {
        #region Helper classes
        /// <summary>
        ///  作业对象，主要用于子进程管理。
        ///  目前版本是只支持作业销毁拥有的子进程退出
        ///  通常可以定义一个全局静态变量使用
        /// </summary>

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateJobObject(IntPtr a, string lpName);
        [DllImport("kernel32.dll")]
        static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, UInt32 cbJobObjectInfoLength);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);
        private IntPtr handle;
        private bool disposed;
        public JobService() {
            handle = CreateJobObject(IntPtr.Zero, null);
            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION {
                LimitFlags = 0x2000
            };
            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
                BasicLimitInformation = info
            };
            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
            if (!SetInformationJobObject(handle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
                throw new Exception(string.Format("Unable to set information.  Error: {0}", Marshal.GetLastWin32Error()));
        }
        /// <summary>
        /// 进程加入到作业对象中
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <returns></returns>
        public bool AddProcess(IntPtr processHandle) {
            return AssignProcessToJobObject(handle, processHandle);
        }

        /// <summary>
        /// 进程加入到作业对象中
        /// </summary>
        /// <param name="processId">进程Id</param>
        /// <returns></returns>
        public bool AddProcess(int processId) {
            return AddProcess(Process.GetProcessById(processId).Handle);
        }
        /// <summary>
        /// 销毁作业对象，手动调用则其拥有的所有进程都会退出
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// 销毁作业对象，手动调用则其拥有的所有进程都会退出
        /// </summary>
        public void Close() {
            CloseHandle(handle);
            handle = IntPtr.Zero;
        }
        private void Dispose(bool disposing) {
            if (disposed)
                return;
            if (disposing) { }
            Close();
            disposed = true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES {
        public UInt32 nLength;
        public IntPtr lpSecurityDescriptor;
        public Int32 bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    public enum JobObjectInfoType {
        AssociateCompletionPortInformation = 7,
        BasicLimitInformation = 2,
        BasicUIRestrictions = 4,
        EndOfJobTimeInformation = 6,
        ExtendedLimitInformation = 9,
        SecurityLimitInformation = 5,
        GroupInformation = 11
    }
    #endregion
}
