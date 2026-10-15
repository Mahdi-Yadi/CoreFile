using System.IO;
using System.IO.Compression;

namespace CoreFile.Services;

public static class ZipArchiveService
{
    public static async Task<string> CompressAsync(List<string> sourcePaths, string targetDirectory)
    {
        return await Task.Run(() =>
        {
            if (sourcePaths.Count == 0) return string.Empty;

            string baseName = Path.GetFileNameWithoutExtension(sourcePaths[0]);
            string zipPath = Path.Combine(targetDirectory, $"{baseName}.zip");

            int count = 1;
            while (File.Exists(zipPath))
            {
                zipPath = Path.Combine(targetDirectory, $"{baseName} ({count++}).zip");
            }

            using var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            foreach (var path in sourcePaths)
            {
                if (File.Exists(path))
                {
                    zipArchive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
                }
                else if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    string rootDirName = dirInfo.Name;

                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(path, file);
                        string entryName = Path.Combine(rootDirName, relative).Replace('\\', '/');
                        zipArchive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                    }
                }
            }

            return zipPath;
        });
    }

    public static async Task<string> ExtractAsync(string zipFilePath, string targetDirectory)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(zipFilePath)) return string.Empty;

            string folderName = Path.GetFileNameWithoutExtension(zipFilePath);
            string destinationFolder = Path.Combine(targetDirectory, folderName);

            int count = 1;
            while (Directory.Exists(destinationFolder))
            {
                destinationFolder = Path.Combine(targetDirectory, $"{folderName} ({count++})");
            }

            Directory.CreateDirectory(destinationFolder);
            ZipFile.ExtractToDirectory(zipFilePath, destinationFolder, overwriteFiles: true);

            return destinationFolder;
        });
    }
}
