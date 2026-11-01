using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UncensoredStudio.Services
{
    /// <summary>
    /// Production-grade Child Process Lifecycle Manager.
    /// Uses Windows Kernel Job Objects (JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE) to guarantee
    /// that whenever UncensoredStudio closes, crashes, or is terminated, all child processes
    /// (including KoboldCPP engine) are guaranteed by the OS kernel to be killed instantly with zero zombie processes.
    /// </summary>
    public static class ChildProcessTracker
    {
        private static readonly IntPtr s_jobHandle;

        static ChildProcessTracker()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            s_jobHandle = CreateJobObject(IntPtr.Zero, null);

            var basicLimitInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK
            };

            var extendedLimitInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = basicLimitInfo
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedLimitInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extendedLimitInfo, extendedLimitInfoPtr, false);
                if (!SetInformationJobObject(s_jobHandle, JobObjectInfoType.ExtendedLimitInformation, extendedLimitInfoPtr, (uint)length))
                {
                    Debug.WriteLine($"Failed to set JobObject limit: {Marshal.GetLastWin32Error()}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(extendedLimitInfoPtr);
            }
        }

        public static void AddProcess(Process process)
        {
            if (s_jobHandle != IntPtr.Zero && process != null && !process.HasExited)
            {
                try
                {
                    AssignProcessToJobObject(s_jobHandle, process.Handle);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to assign process to JobObject: {ex.Message}");
                }
            }
        }

        #region Native Win32 API Interop
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const uint JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK = 0x00001000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }
        #endregion
    }
}
