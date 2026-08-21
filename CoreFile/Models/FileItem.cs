using CommunityToolkit.Mvvm.ComponentModel;

namespace CoreFile.Models;

public partial class FileItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string fullPath = string.Empty;

    [ObservableProperty]
    private string extension = string.Empty;

    [ObservableProperty]
    private string type = string.Empty;

    [ObservableProperty]
    private string sizeText = string.Empty;

    [ObservableProperty]
    private DateTime createdDate;

    [ObservableProperty]
    private DateTime modifiedDate;

    [ObservableProperty]
    private bool isDirectory;

    [ObservableProperty]
    private string icon = string.Empty;
}