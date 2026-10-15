
namespace CoreFile.Models;

public enum ConflictResolution
{
    Replace,
    Skip,
    KeepBoth,
    Cancel
}

public class ConflictResolutionResult
{
    public ConflictResolution Resolution { get; set; }
    public bool ApplyToAll { get; set; }
}

public class ConflictFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public long SourceSizeBytes { get; set; }
    public long DestinationSizeBytes { get; set; }
    public DateTime SourceModified { get; set; }
    public DateTime DestinationModified { get; set; }
}