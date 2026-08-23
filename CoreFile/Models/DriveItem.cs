using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
namespace CoreFile.Models;
public partial class DriveItem : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string fullPath = string.Empty;
    [ObservableProperty] private string label = string.Empty;
    [ObservableProperty] private long totalSize;
    [ObservableProperty] private long freeSpace;
    [ObservableProperty] private double usedPercentage;
    [ObservableProperty] private ImageSource? icon;
}