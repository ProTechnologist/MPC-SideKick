using System.Windows;
using Microsoft.Win32;

namespace MPC.SideKick
{
    public partial class SettingsWindow : Window
    {
        public AppSettings CurrentSettings { get; private set; }

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            CurrentSettings = settings;
            MpcPathBox.Text = settings.MediaPlayerPath;
            RememberFolderCheck.IsChecked = settings.RememberLastFolder;
        }

        private void BrowseMpc_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                MpcPathBox.Text = dialog.FileName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            CurrentSettings.MediaPlayerPath = MpcPathBox.Text;
            CurrentSettings.RememberLastFolder = RememberFolderCheck.IsChecked ?? false;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
