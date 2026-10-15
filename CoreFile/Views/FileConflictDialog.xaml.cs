using CoreFile.Models;
using System.IO;
using System.Windows;

namespace CoreFile.Views;

public partial class FileConflictDialog : Window
{
    public ConflictResolutionResult Result { get; private set; } = new() { Resolution = ConflictResolution.Skip };

    public FileConflictDialog(ConflictFileInfo info)
    {
        InitializeComponent();

        TxtFileName.Text = Path.GetFileName(info.DestinationPath);
        TxtSourceSize.Text = FormatSize(info.SourceSizeBytes);
        TxtSourceDate.Text = info.SourceModified.ToString("yyyy/MM/dd HH:mm");

        TxtDestSize.Text = FormatSize(info.DestinationSizeBytes);
        TxtDestDate.Text = info.DestinationModified.ToString("yyyy/MM/dd HH:mm");
    }

    private void BtnReplace_Click(object sender, RoutedEventArgs e)
    {
        Result = new ConflictResolutionResult
        {
            Resolution = ConflictResolution.Replace,
            ApplyToAll = ChkApplyToAll.IsChecked == true
        };
        DialogResult = true;
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        Result = new ConflictResolutionResult
        {
            Resolution = ConflictResolution.Skip,
            ApplyToAll = ChkApplyToAll.IsChecked == true
        };
        DialogResult = true;
    }

    private void BtnKeepBoth_Click(object sender, RoutedEventArgs e)
    {
        Result = new ConflictResolutionResult
        {
            Resolution = ConflictResolution.KeepBoth,
            ApplyToAll = ChkApplyToAll.IsChecked == true
        };
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Result = new ConflictResolutionResult
        {
            Resolution = ConflictResolution.Cancel,
            ApplyToAll = false
        };
        DialogResult = false;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024):F1} MB";
        return $"{bytes / (1024d * 1024 * 1024):F1} GB";
    }
}