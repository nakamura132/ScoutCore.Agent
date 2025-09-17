using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ScoutCore.Agent.Journal;

/// <summary>
/// NTFS の安定 ID (VolumeSerial + FileIndex) を FileKey として取得
/// 取得できない場合は null を返す（例：非 NTFS, ファイル不存在 等）
/// </summary>
public static class FileKeyUtil
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, int dwDesiredAccess, FileShare dwShareMode,
        IntPtr lpSecurityAttributes, FileMode dwCreationDisposition,
        FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    public static string? TryGetFileKey(string path)
    {
        try
        {
            using var h = CreateFileW(path, 0 /* no access */, FileShare.ReadWrite | FileShare.Delete,
                                      IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
            if (h.IsInvalid) return null;
            if (!GetFileInformationByHandle(h, out var info)) return null;

            ulong idx = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
            return $"{info.VolumeSerialNumber:X8}:{idx:X16}";
        }
        catch
        {
            return null;
        }
    }
}
