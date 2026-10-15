using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoreFile.Services;
public class IconService
{
    private static readonly ConcurrentDictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);

    #region Win32 API
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
    #endregion

    public static ImageSource GetIcon(string path, bool isDirectory, bool isDrive = false)
    {
        // رفع باگ: کلید کش برای درایو، پوشه و پسوند فایل کاملاً تفکیک شد
        string cacheKey = isDrive
            ? $"___DRIVE_{path}___"
            : (isDirectory ? "___FOLDER___" : Path.GetExtension(path));

        if (!string.IsNullOrEmpty(cacheKey) && IconCache.TryGetValue(cacheKey, out var cachedIcon))
        {
            return cachedIcon;
        }

        var shinfo = new SHFILEINFO();
        uint flags = SHGFI_ICON | SHGFI_SMALLICON;
        uint dwAttr = FILE_ATTRIBUTE_NORMAL;

        if (isDrive)
        {
            // برای درایو آیکون واقعی خود درایو را از مسیر می‌خوانیم
            flags &= ~SHGFI_USEFILEATTRIBUTES;
        }
        else
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
            if (isDirectory)
            {
                dwAttr = FILE_ATTRIBUTE_DIRECTORY;
            }
        }

        SHGetFileInfo(path, dwAttr, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

        if (shinfo.hIcon == IntPtr.Zero)
        {
            return null!;
        }

        try
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            imageSource.Freeze();

            if (!string.IsNullOrEmpty(cacheKey))
            {
                IconCache[cacheKey] = imageSource;
            }

            return imageSource;
        }
        finally
        {
            DestroyIcon(shinfo.hIcon);
        }
    }
}