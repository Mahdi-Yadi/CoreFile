using System.IO;

namespace CoreFile.Services;

public interface IUndoableAction
{
    string Description { get; }
    Task UndoAsync();
    Task RedoAsync();
}

// ۱. اکشن تغییر نام (Rename)
public class RenameAction(string oldPath, string newPath) : IUndoableAction
{
    public string Description => $"Rename '{Path.GetFileName(oldPath)}' to '{Path.GetFileName(newPath)}'";

    public Task UndoAsync()
    {
        return Task.Run(() =>
        {
            if (File.Exists(newPath))
                File.Move(newPath, oldPath);
            else if (Directory.Exists(newPath))
                Directory.Move(newPath, oldPath);
        });
    }

    public Task RedoAsync()
    {
        return Task.Run(() =>
        {
            if (File.Exists(oldPath))
                File.Move(oldPath, newPath);
            else if (Directory.Exists(oldPath))
                Directory.Move(oldPath, newPath);
        });
    }
}

// ۲. اکشن ساخت پوشه (Create Folder)
public class CreateFolderAction(string folderPath) : IUndoableAction
{
    public string Description => $"Create Folder '{Path.GetFileName(folderPath)}'";

    public Task UndoAsync()
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(folderPath))
            {
                RecycleBinService.SendToRecycleBin(folderPath);
            }
        });
    }

    public Task RedoAsync()
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        });
    }
}

// ۳. اکشن جابه‌جایی یا کپی (Move / Copy)
public class TransferAction(List<(string Source, string Destination)> items, OperationType type) : IUndoableAction
{
    public string Description => type == OperationType.Cut
        ? $"Move {items.Count} item(s)"
        : $"Copy {items.Count} item(s)";

    public Task UndoAsync()
    {
        return Task.Run(() =>
        {
            foreach (var (src, dst) in items)
            {
                if (type == OperationType.Cut)
                {
                    // برگرداندن فایل جابجا شده به مکان مبدا
                    if (File.Exists(dst))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(src)!);
                        File.Move(dst, src, overwrite: true);
                    }
                    else if (Directory.Exists(dst))
                    {
                        Directory.Move(dst, src);
                    }
                }
                else // اگر کپی بود، فایل ساخته شده در مقصد حذف می‌شود
                {
                    if (File.Exists(dst))
                        RecycleBinService.SendToRecycleBin(dst);
                    else if (Directory.Exists(dst))
                        RecycleBinService.SendToRecycleBin(dst);
                }
            }
        });
    }

    public Task RedoAsync()
    {
        return Task.Run(() =>
        {
            foreach (var (src, dst) in items)
            {
                if (type == OperationType.Cut)
                {
                    if (File.Exists(src))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                        File.Move(src, dst, overwrite: true);
                    }
                    else if (Directory.Exists(src))
                    {
                        Directory.Move(src, dst);
                    }
                }
            }
        });
    }
}

// مدیریت پشته‌های Undo و Redo
public class UndoRedoService
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Record(IUndoableAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear(); // با انجام عملیات جدید، پشته Redo ریست می‌شود
    }

    public async Task<string?> UndoAsync()
    {
        if (!CanUndo) return null;

        var action = _undoStack.Pop();
        await action.UndoAsync();
        _redoStack.Push(action);

        return action.Description;
    }

    public async Task<string?> RedoAsync()
    {
        if (!CanRedo) return null;

        var action = _redoStack.Pop();
        await action.RedoAsync();
        _undoStack.Push(action);

        return action.Description;
    }
}
// اکشن حذف و بازگردانی از Recycle Bin
public class DeleteAction(List<string> filePaths) : IUndoableAction
{
    public string Description => $"Delete {filePaths.Count} item(s)";

    public Task UndoAsync()
    {
        return Task.Run(() =>
        {
            foreach (var path in filePaths)
            {
                RestoreItemFromRecycleBin(path);
            }
        });
    }

    public Task RedoAsync()
    {
        return Task.Run(() =>
        {
            foreach (var path in filePaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    RecycleBinService.SendToRecycleBin(path);
                }
            }
        });
    }

    private static void RestoreItemFromRecycleBin(string originalPath)
    {
        try
        {
            // شل ویندوز برای دسترسی به محتویات سطل بازیافت
            Type? shellAppType = Type.GetTypeFromProgID("Shell.Application");
            if (shellAppType == null) return;

            dynamic shell = Activator.CreateInstance(shellAppType)!;
            // عدد 10 شناسه پوشه مخصوص Recycle Bin در ویندوز است (ssfBITBUCKET)
            dynamic recycleBin = shell.NameSpace(10);

            string targetFileName = Path.GetFileName(originalPath);

            foreach (var item in recycleBin.Items())
            {
                // تطبیق نام فایل حذف‌شده
                if (string.Equals((string)item.Name, targetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    // بررسی مسیر اصلی فایل قبل از حذف (Original Location)
                    string itemOriginalLocation = recycleBin.GetDetailsOf(item, 1); // ستون آدرس قبلی در ویندوز
                    string? parentDir = Path.GetDirectoryName(originalPath);

                    if (string.IsNullOrEmpty(itemOriginalLocation) ||
                        string.Equals(itemOriginalLocation, parentDir, StringComparison.OrdinalIgnoreCase))
                    {
                        // فراخوانی دستور Restore بومی سیستم‌عامل روی آیتم سطل زباله
                        foreach (var verb in item.Verbs())
                        {
                            string verbName = ((string)verb.Name).Replace("&", "");
                            if (verbName.Equals("Restore", StringComparison.OrdinalIgnoreCase) ||
                                verbName.Equals("بازیابی", StringComparison.OrdinalIgnoreCase))
                            {
                                verb.DoIt();
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }
        catch { }
    }
}
// اکشن تغییر نام گروهی در سیستم Undo/Redo
public class BatchRenameAction(List<(string OldPath, string NewPath)> pairs) : IUndoableAction
{
    public string Description => $"Batch Rename {pairs.Count} item(s)";

    public Task UndoAsync()
    {
        return Task.Run(() =>
        {
            foreach (var (oldPath, newPath) in pairs)
            {
                if (File.Exists(newPath))
                    File.Move(newPath, oldPath);
                else if (Directory.Exists(newPath))
                    Directory.Move(newPath, oldPath);
            }
        });
    }

    public Task RedoAsync()
    {
        return Task.Run(() =>
        {
            foreach (var (oldPath, newPath) in pairs)
            {
                if (File.Exists(oldPath))
                    File.Move(oldPath, newPath);
                else if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);
            }
        });
    }
}