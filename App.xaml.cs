using System.Configuration;
using System.Data;
using System.Windows;

namespace MPC_SideKick;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private PlaylistWindow? _playlistWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mainWindow = new MainWindow();
        _mainWindow.Show();

        _playlistWindow = new PlaylistWindow();
        _playlistWindow.Show();
    }
}
