using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CoreFile.Services;

public static class ShellContextMenuService
{
    #region Win32 Constants & APIs

    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXPLORE = 0x00000004;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const int SW_SHOWNORMAL = 1;

    private const uint WM_INITMENUPOPUP = 0x0117;
    private const uint WM_MEASUREITEM = 0x002C;
    private const uint WM_DRAWITEM = 0x002B;
    private const uint WM_MENUCHAR = 0x0120;
    private const uint WM_NULL = 0x0000;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ILCreateFromPath(string pszPath);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHBindToParent(
        IntPtr pidl,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellFolder ppv,
        out IntPtr ppidlLast);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        IntPtr hmenu,
        uint fuFlags,
        int x,
        int y,
        IntPtr hwnd,
        IntPtr lptpm);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    #endregion

    #region COM Interfaces

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, string pszDisplayName, uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
        [PreserveSig]
        int GetUIObjectOf(
            IntPtr hwndOwner,
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            IntPtr rgfReserved,
            [MarshalAs(UnmanagedType.Interface)] out IContextMenu ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl, string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig]
        int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder commandstring, uint cch);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214f4-0000-0000-c000-000000000046")]
    private interface IContextMenu2 : IContextMenu
    {
        [PreserveSig]
        new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig]
        new int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig]
        new int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder commandstring, uint cch);
        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
    private interface IContextMenu3 : IContextMenu2
    {
        [PreserveSig]
        new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig]
        new int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig]
        new int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder commandstring, uint cch);
        [PreserveSig]
        new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        [PreserveSig]
        int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public string lpParameters;
        public string lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public string lpTitle;
        public IntPtr lpVerbW;
        public string lpParametersW;
        public string lpDirectoryW;
        public string lpTitleW;
        public POINT ptInvoke;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IID_IContextMenu = new("000214e4-0000-0000-c000-000000000046");

    #endregion

    public static void ShowContextMenu(Window owner, IReadOnlyList<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0) return;

        var validPaths = filePaths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (validPaths.Count == 0) return;

        IntPtr hwndOwner = new WindowInteropHelper(owner).Handle;
        var fullPidls = new List<IntPtr>();
        var childPidls = new List<IntPtr>();
        IShellFolder? parentFolder = null;
        IContextMenu? contextMenu = null;
        IntPtr hMenu = IntPtr.Zero;

        HwndSource? hwndSource = null;
        HwndSourceHook? hook = null;

        try
        {
            // ۱. تبدیل مسیر تک‌تک آیتم‌ها به PIDL و استخراج پوینتر نسبی بدون نابود کردن حافظه آنها
            foreach (var path in validPaths)
            {
                IntPtr fullPidl = ILCreateFromPath(path);
                if (fullPidl != IntPtr.Zero)
                {
                    fullPidls.Add(fullPidl);

                    int hr = SHBindToParent(fullPidl, IID_IShellFolder, out var curParent, out IntPtr relPidl);
                    if (hr == 0 && relPidl != IntPtr.Zero)
                    {
                        parentFolder ??= curParent;
                        childPidls.Add(relPidl);
                    }
                }
            }

            if (parentFolder == null || childPidls.Count == 0) return;

            // ۲. دریافت اینترفیس IContextMenu از خود پوشه والد حقیقی
            int getUiHr = parentFolder.GetUIObjectOf(
                hwndOwner,
                (uint)childPidls.Count,
                childPidls.ToArray(),
                IID_IContextMenu,
                IntPtr.Zero,
                out contextMenu);

            if (getUiHr != 0 || contextMenu == null) return;

            hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            // ۳. پر کردن منو با دستورات ویندوز و افزونه‌ها
            contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_EXPLORE | CMF_NORMAL);

            // ۴. اضافه کردن هوک برای پشتیبانی از ساب‌منوها (مثل Open With یا گزینه‌های WinRAR)
            var cm2 = contextMenu as IContextMenu2;
            var cm3 = contextMenu as IContextMenu3;

            hwndSource = HwndSource.FromHwnd(hwndOwner);
            hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                uint uMsg = (uint)msg;
                if (uMsg == WM_INITMENUPOPUP || uMsg == WM_DRAWITEM || uMsg == WM_MEASUREITEM || uMsg == WM_MENUCHAR)
                {
                    if (cm3 != null)
                    {
                        if (cm3.HandleMenuMsg2(uMsg, wParam, lParam, out IntPtr lResult) == 0)
                        {
                            handled = true;
                            return lResult;
                        }
                    }
                    else if (cm2 != null)
                    {
                        if (cm2.HandleMenuMsg(uMsg, wParam, lParam) == 0)
                        {
                            handled = true;
                            return IntPtr.Zero;
                        }
                    }
                }
                return IntPtr.Zero;
            };

            hwndSource?.AddHook(hook);

            // ۵. دریافت موقعیت دقیق کرسر موس در کل مانیتور
            GetCursorPos(out POINT cursorPt);
            SetForegroundWindow(hwndOwner);

            uint selectedCmd = TrackPopupMenuEx(
                hMenu,
                TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_LEFTALIGN,
                cursorPt.x,
                cursorPt.y,
                hwndOwner,
                IntPtr.Zero);

            PostMessage(hwndOwner, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            // ۶. اجرای فرمان انتخابی
            if (selectedCmd > 0)
            {
                var invokeInfo = new CMINVOKECOMMANDINFOEX
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                    fMask = 0x00004000 | 0x00000100, // CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE
                    hwnd = hwndOwner,
                    lpVerb = (IntPtr)(selectedCmd - 1),
                    lpVerbW = (IntPtr)(selectedCmd - 1),
                    nShow = SW_SHOWNORMAL,
                    ptInvoke = cursorPt
                };

                contextMenu.InvokeCommand(ref invokeInfo);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Context menu error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (hook != null && hwndSource != null)
            {
                hwndSource.RemoveHook(hook);
            }

            if (hMenu != IntPtr.Zero) DestroyMenu(hMenu);

            foreach (var pidl in fullPidls)
            {
                ILFree(pidl);
            }

            if (contextMenu != null) Marshal.ReleaseComObject(contextMenu);
            if (parentFolder != null) Marshal.ReleaseComObject(parentFolder);
        }
    }
}