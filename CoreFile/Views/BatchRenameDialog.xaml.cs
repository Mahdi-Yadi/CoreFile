using CoreFile.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace CoreFile.Views;

public class RenamePreviewItem
{
    public string FullPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
}

public partial class BatchRenameDialog : Window
{
    private readonly List<FileItem> _items;
    public ObservableCollection<RenamePreviewItem> Previews { get; } = [];
    public List<(string OldPath, string NewPath)> RenamedPairs { get; private set; } = [];

    public BatchRenameDialog(List<FileItem> items)
    {
        InitializeComponent();
        _items = items;
        PreviewList.ItemsSource = Previews;
        UpdatePreview();
    }

    private void Rule_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        Previews.Clear();

        string prefix = TxtPrefix?.Text ?? string.Empty;
        string suffix = TxtSuffix?.Text ?? string.Empty;
        string find = TxtFind?.Text ?? string.Empty;
        string replace = TxtReplace?.Text ?? string.Empty;

        int index = 1;
        foreach (var item in _items)
        {
            string ext = item.IsDirectory ? string.Empty : item.Extension;
            string nameWithoutExt = item.IsDirectory ? item.Name : Path.GetFileNameWithoutExtension(item.FullPath);

            // ۱. جایگزینی عبارت
            if (!string.IsNullOrEmpty(find))
            {
                nameWithoutExt = nameWithoutExt.Replace(find, replace);
            }

            // ۲. پیشوند و پسوند
            string finalName = $"{prefix}{nameWithoutExt}{suffix}{ext}";

            Previews.Add(new RenamePreviewItem
            {
                FullPath = item.FullPath,
                OriginalName = item.Name,
                NewName = finalName
            });
            index++;
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        RenamedPairs.Clear();

        foreach (var p in Previews)
        {
            if (string.Equals(p.OriginalName, p.NewName)) continue;

            string? dir = Path.GetDirectoryName(p.FullPath);
            if (dir == null) continue;

            string targetPath = Path.Combine(dir, p.NewName);
            RenamedPairs.Add((p.FullPath, targetPath));
        }

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}