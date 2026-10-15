using CoreFile.Models;
using System.IO;
namespace CoreFile.Services;
public class FileSystemService
{
    public IEnumerable<FileItem> GetDrives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
                continue;

            long totalBytes = drive.TotalSize;
            long freeBytes = drive.AvailableFreeSpace;
            long usedBytes = totalBytes - freeBytes;

            double usedPercent = totalBytes > 0 ? ((double)usedBytes / totalBytes) * 100 : 0;
            string freeText = FormatSize(freeBytes);
            string totalText = FormatSize(totalBytes);

            yield return new FileItem
            {
                Name = $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})",
                FullPath = drive.RootDirectory.FullName,
                Type = "Drive",
                IsDirectory = true,
                IsDrive = true,
                DriveUsedPercentage = usedPercent,
                DriveUsageText = $"{freeText} free of {totalText}",
                IsDriveNearFull = usedPercent >= 90.0,
                Icon = IconService.GetIcon(drive.Name, isDirectory: true, isDrive: true)
            };
        }
    }

    // نمونه بررسی در متد ساخت FileItem
    public FileItem CreateFromFileSystemInfo(FileSystemInfo info)
    {
        bool isDir = (info.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
        return new FileItem
        {
            Name = info.Name,
            FullPath = info.FullName,
            IsDirectory = isDir,
            Extension = isDir ? string.Empty : info.Extension,
            // سایر فیلدها...
        };
    }

    public IEnumerable<FileItem> GetItems(string path)
    {
        var result = new List<FileItem>();

        try
        {
            var directory = new DirectoryInfo(path);

            foreach (var directoryInfo in directory.GetDirectories())
            {
                try
                {
                    // در بخش پوشه‌ها:
                    result.Add(new FileItem
                    {
                        Name = directoryInfo.Name,
                        FullPath = directoryInfo.FullName,
                        Type = "Folder",
                        IsDirectory = true,
                        SizeText = "--",
                        Icon = IconService.GetIcon(directoryInfo.FullName, isDirectory: true),
                        CreatedDate = directoryInfo.CreationTime,
                        ModifiedDate = directoryInfo.LastWriteTime
                    });
                }
                catch
                {
                    // یک Folder ممکن است Permission نداشته باشد.
                }
            }

            foreach (var file in directory.GetFiles())
            {
                try
                {
                    // در بخش فایل‌ها:
                    result.Add(new FileItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        Extension = file.Extension,
                        Type = string.IsNullOrWhiteSpace(file.Extension)
                            ? "File"
                            : file.Extension.TrimStart('.').ToUpperInvariant(),
                        SizeText = FormatSize(file.Length),
                        IsDirectory = false,
                        Icon = IconService.GetIcon(file.FullName, isDirectory: false),
                        CreatedDate = file.CreationTime,
                        ModifiedDate = file.LastWriteTime,
                        SizeBytes = file.Length,
                    });
                }
                catch
                {
                    // ممکن است فایل قابل دسترسی نباشد.
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }

        return result
            .OrderByDescending(x => x.IsDirectory)
            .ThenBy(x => x.Name);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:F1} KB";

        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024d * 1024):F1} MB";

        if (bytes < 1024L * 1024 * 1024 * 1024)
            return $"{bytes / (1024d * 1024 * 1024):F1} GB";

        return $"{bytes / (1024d * 1024 * 1024 * 1024):F1} TB";
    }

    public async Task<(long TotalBytes, int FileCount)> CalculateDirectorySizeAsync(string directoryPath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            long totalBytes = 0;
            int fileCount = 0;

            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint // جلوگیری از لوپ در Symlinkها
                };

                var dirInfo = new DirectoryInfo(directoryPath);
                foreach (var file in dirInfo.EnumerateFiles("*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    totalBytes += file.Length;
                    fileCount++;
                }
            }
            catch { }

            return (totalBytes, fileCount);
        }, cancellationToken);
    }

}