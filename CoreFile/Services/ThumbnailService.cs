using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace CoreFile.Services;

public static class ThumbnailService
{
    // شناسه دقیق اینترفیس IShellItemImageFactory در ویندوز
    private static readonly Guid IShellItemImageFactoryGuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage([In] SIZE size, [In] int flags, [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>
    /// استخراج تامبنیل: ابتدا تلاش از طریق لود سبک عکس، در غیر این صورت استفاده از شل ویندوز
    /// </summary>
    public static BitmapSource? GetThumbnail(string filePath, int targetSize = 120)
    {
        if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
            return null;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        // روش مستقیم، پرسرعت و تضمینی برای انواع تصاویر
        string[] directImages = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".ico", ".gif"];
        if (Array.IndexOf(directImages, ext) >= 0)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.DecodePixelWidth = targetSize; // فقط سایز مورد نیاز لود می‌شود تا رم مصرف نشود
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                // در صورت بروز خطا به روش Shell سویچ می‌کند
            }
        }

        // روش ویندوز شل (برای فایل‌های ویدیویی، پی‌دی‌اف و بقیه موارد)
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, IShellItemImageFactoryGuid, out var factory);
            if (hr != 0 || factory == null) return null;

            // پرچم 0x01 = SIIGBF_BIGGERSIZEOK
            int imgResult = factory.GetImage(new SIZE(targetSize, targetSize), 0x01, out hBitmap);
            if (imgResult != 0 || hBitmap == IntPtr.Zero) return null;

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
        }
    }
}