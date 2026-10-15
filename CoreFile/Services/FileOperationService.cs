using CoreFile.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreFile.Services;

public enum OperationType { Copy, Cut }
public record ClipboardState(List<string> SourcePaths, OperationType Type);

public class FileOperationService
{
    public async Task CopyOrMoveAsync(
        List<string> sources,
        string targetDirectory,
        OperationType operation,
        IProgress<double>? progress,
        CancellationToken cancellationToken,
        Func<ConflictFileInfo, Task<ConflictResolutionResult>>? resolveConflictAsync = null)
    {
        await Task.Run(async () =>
        {
            var allFiles = GetSourceFiles(sources);
            long totalBytes = allFiles.Sum(f => new FileInfo(f).Length);
            long copiedBytes = 0;

            ConflictResolutionResult? globalResolution = null;

            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = GetRelativePath(sources, file);
                string destinationPath = Path.Combine(targetDirectory, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                // بررسی وجود فایل تکراری در مقصد
                if (File.Exists(destinationPath))
                {
                    ConflictResolution resolution = ConflictResolution.Replace;

                    if (globalResolution != null && globalResolution.ApplyToAll)
                    {
                        resolution = globalResolution.Resolution;
                    }
                    else if (resolveConflictAsync != null)
                    {
                        var srcInfo = new FileInfo(file);
                        var dstInfo = new FileInfo(destinationPath);

                        var conflictInfo = new ConflictFileInfo
                        {
                            FileName = Path.GetFileName(destinationPath),
                            SourcePath = file,
                            DestinationPath = destinationPath,
                            SourceSizeBytes = srcInfo.Length,
                            DestinationSizeBytes = dstInfo.Length,
                            SourceModified = srcInfo.LastWriteTime,
                            DestinationModified = dstInfo.LastWriteTime
                        };

                        var result = await resolveConflictAsync(conflictInfo);

                        if (result.ApplyToAll)
                        {
                            globalResolution = result;
                        }

                        resolution = result.Resolution;
                    }

                    if (resolution == ConflictResolution.Cancel)
                    {
                        throw new OperationCanceledException();
                    }
                    else if (resolution == ConflictResolution.Skip)
                    {
                        copiedBytes += new FileInfo(file).Length;
                        progress?.Report((double)copiedBytes / Math.Max(1, totalBytes) * 100);
                        continue;
                    }
                    else if (resolution == ConflictResolution.KeepBoth)
                    {
                        destinationPath = GenerateUniqueFilePath(destinationPath);
                    }
                }

                // انجام کپی یا انتقال
                if (operation == OperationType.Copy)
                {
                    await CopyFileWithProgressAsync(file, destinationPath, (b) =>
                    {
                        copiedBytes += b;
                        progress?.Report((double)copiedBytes / Math.Max(1, totalBytes) * 100);
                    }, cancellationToken);
                }
                else // Cut / Move
                {
                    try
                    {
                        File.Move(file, destinationPath, overwrite: true);
                        copiedBytes += new FileInfo(destinationPath).Length;
                        progress?.Report((double)copiedBytes / Math.Max(1, totalBytes) * 100);
                    }
                    catch (IOException)
                    {
                        await CopyFileWithProgressAsync(file, destinationPath, (b) =>
                        {
                            copiedBytes += b;
                            progress?.Report((double)copiedBytes / Math.Max(1, totalBytes) * 100);
                        }, cancellationToken);
                        File.Delete(file);
                    }
                }
            }

            if (operation == OperationType.Cut)
            {
                foreach (var source in sources.Where(Directory.Exists))
                {
                    try { Directory.Delete(source, recursive: true); } catch { }
                }
            }
        }, cancellationToken);
    }

    private static string GenerateUniqueFilePath(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath)!;
        string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        string ext = Path.GetExtension(filePath);

        int count = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{nameWithoutExt} ({count++}){ext}");
        } while (File.Exists(newPath));

        return newPath;
    }

    private static async Task CopyFileWithProgressAsync(
        string source,
        string destination,
        Action<int> onChunkCopied,
        CancellationToken ct)
    {
        const int bufferSize = 81920;
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