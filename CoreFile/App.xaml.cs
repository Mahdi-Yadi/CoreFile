using CoreFile.Services;
using System.Windows;
namespace CoreFile;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettingsService.Load();
        var themeUri = settings.IsDarkMode
            ? new Uri("Themes/ColorsDark.xaml", UriKind.Relative)
            : new Uri("Themes/ColorsLight.xaml", UriKind.Relative);

        var newThemeDict = new ResourceDictionary { Source = themeUri };

        for (int i = 0; i < Resources.MergedDictionaries.Count; i++)
        {
            var dict = Resources.MergedDictionaries[i];
            if (dict.Source != null && dict.Source.OriginalString.Contains("Colors", StringComparison.OrdinalIgnoreCase))
            {
                Resources.MergedDictionaries[i] = newThemeDict;
                return;
            }
        }
        Resources.MergedDictionaries.Add(newThemeDict);
    }
}