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

            yield return new FileItem
            {
                Name = $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})",
                FullPath = drive.RootDirectory.FullName,
                Type = "Drive",
                IsDirectory = true,
                Icon = "💽"
            };
        }
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
                    result.Add(new FileItem
                    {
                        Name = directoryInfo.Name,
                        FullPath = directoryInfo.FullName,
                        Type = "Folder",
                        IsDirectory = true,
                        Icon = "📁",
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
                        Icon = "📄",

                        CreatedDate = file.CreationTime,
                        ModifiedDate = file.LastWriteTime
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
}