using System.Runtime.InteropServices;
namespace CoreFile.Services;
public class RecycleBinService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040; // انتقال به Recycle Bin
    private const ushort FOF_NOCONFIRMATION = 0x0010; // عدم نمایش Confirm ویندوز (اختیاری)

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    public static bool SendToRecycleBin(string path)
    {
        try
        {
            var fileop = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0', // نیازمند double null-terminator
                fFlags = FOF_ALLOWUNDO
            };
            return SHFileOperation(ref fileop) == 0;
        }
        catch
        {
            return false;
        }
    }
}