using System.IO;

namespace CoreFile.Services;

public enum OperationType { Copy, Cut }

public record ClipboardState(List<string> SourcePaths, OperationType Type);

public class FileOperationService
{
    // کپی/انتقال پوشه و فایل به صورت کاملاً Async همراه با Progress
    public async Task CopyOrMoveAsync(
        List<string> sources,
        string targetDirectory,
        OperationType operation,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Run(async () =>
        {
            var allFiles = GetSourceFiles(sources);
            long totalBytes = allFiles.Sum(f => new FileInfo(f).Length);
            long copiedBytes = 0;

            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = GetRelativePath(sources, file);
                string destinationPath = Path.Combine(targetDirectory, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                if (operation == OperationType.Copy)
                {
                    await CopyFileWithProgressAsync(file, destinationPath, (b) =>
                    {
                        copiedBytes += b;
                        progress?.Report((double)copiedBytes / totalBytes * 100);
                    }, cancellationToken);
                }
                else // Cut / Move
                {
                    File.Move(file, destinationPath, overwrite: true);
                    copiedBytes += new FileInfo(destinationPath).Length;
                    progress?.Report((double)copiedBytes / totalBytes * 100);
                }
            }

            // اگر Cut بود، دایرکتوری‌های خالی شده مبدا پاک شوند
            if (operation == OperationType.Cut)
            {
                foreach (var source in sources.Where(Directory.Exists))
                {
                    Directory.Delete(source, recursive: true);
                }
            }
        }, cancellationToken);
    }

    private static async Task CopyFileWithProgressAsync(
        string source,
        string destination,
        Action<int> onChunkCopied,
        CancellationToken ct)
    {
        const int bufferSize = 81920; // 80 KB Buffer
        var buffer = new byte[bufferSize];

        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
        await using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);

        int bytesRead;
        while ((bytesRead = await sourceStream.ReadAsync(buffer, ct)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            onChunkCopied(bytesRead);
        }
    }

    private static List<string> GetSourceFiles(List<string> paths)
    {
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
                files.Add(path);
            else if (Directory.Exists(path))
                files.AddRange(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));
        }
        return files;
    }

    private static string GetRelativePath(List<string> sources, string filePath)
    {
        foreach (var src in sources)
        {
            if (filePath.StartsWith(src, StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.Exists(src) ? src : Path.GetDirectoryName(src)!;
                return Path.GetRelativePath(parent, filePath);
            }
        }
        return Path.GetFileName(filePath);
    }
}