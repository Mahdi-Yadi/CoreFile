using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoreFile.Services;

public class IconService
{
    // Caching برای جلوگیری از فراخوانی تکراری Win32 API
    private static readonly ConcurrentDictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);

    #region Win32 API Interop
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
    private const uint SHGFI_LARGEICON = 0x000000000; // آیکون بزرگ (32x32)
    private const uint SHGFI_SMALLICON = 0x000000001; // آیکون کوچک (16x16)
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
    #endregion

    public static ImageSource GetIcon(string path, bool isDirectory, bool isSmall = true)
    {
        string extension = isDirectory ? "folder_dir" : Path.GetExtension(path);

        // اگر آیکون پسوند قبلاً کش شده، از حافظه بخوان
        if (!string.IsNullOrEmpty(extension) && IconCache.TryGetValue(extension, out var cachedIcon))
        {
            return cachedIcon;
        }

        var shinfo = new SHFILEINFO();
        uint flags = SHGFI_ICON | (isSmall ? SHGFI_SMALLICON : SHGFI_LARGEICON);
        uint dwAttr = FILE_ATTRIBUTE_NORMAL;

        // برای بالا بردن سرعت، بدون لمس دیسک و فقط بر اساس Attribute آیکون را استخراج می‌کنیم
        flags |= SHGFI_USEFILEATTRIBUTES;

        if (isDirectory)
        {
            dwAttr = FILE_ATTRIBUTE_DIRECTORY;
        }

        IntPtr hImg = SHGetFileInfo(path, dwAttr, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

        if (shinfo.hIcon == IntPtr.Zero)
        {
            return null!;
        }

        try
        {
            // تبدیل HICON به WPF BitmapSource
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            imageSource.Freeze(); // جهت استفاده آزادانه در Threadهای مختلف WPF

            if (!string.IsNullOrEmpty(extension))
            {
                IconCache[extension] = imageSource;
            }

            return imageSource;
        }
        finally
        {
            // آزاد‌سازی منابع سیستم‌عامل
            DestroyIcon(shinfo.hIcon);
        }
    }
}