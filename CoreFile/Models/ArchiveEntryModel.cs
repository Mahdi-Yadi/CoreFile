namespace CoreFile.Models;

public class ArchiveEntryModel
{
    public string Name { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
}
