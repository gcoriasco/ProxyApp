using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Management;
using System.Security.Principal;
//using System.DirectoryServices.AccountManagement;

namespace ProxyApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int proxyBitMask = 2;  // 2: Manual proxy - 4: Automatic configuration script
        const string registryHive = "HKEY_USERS";
        const string registryUserKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections";
        const string registryValueName = "DefaultConnectionSettings";
        readonly string registryKeyFullPath;
        readonly ManagementEventWatcher watcher;
        bool? lastProxyEnabled = null;

        public MainWindow()
        {
            InitializeComponent();

            Command.ExecutionHandler = CommandExecutionHandler;
            var sid = WindowsIdentity.GetCurrent().User.Value;
            //string sid = UserPrincipal.Current.Sid.ToString();
            string registryKeyPath = sid + @"\" + registryUserKeyPath;
            registryKeyFullPath = $"{registryHive}\\{registryKeyPath}";
            WqlEventQuery query = new WqlEventQuery($"SELECT * FROM RegistryKeyChangeEvent WHERE Hive = '{registryHive}' AND KeyPath = '{registryKeyPath.Replace("\\", "\\\\")}'");
            watcher = new ManagementEventWatcher(query);
            watcher.EventArrived += Watcher_EventArrived;
            watcher.Start();

            ProcessProxyStatus();
        }

        private void CommandExecutionHandler(object parameter)
        {
            if (parameter?.ToString() == "invert")
            {
                IsProxyEnabled = !IsProxyEnabled;
            }
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                Dispatcher.Invoke(new Action(() => ProcessProxyStatus()));
            }
        }

        private void ProcessProxyStatus()
        {
            string message;
            bool proxyEnabled = IsProxyEnabled;
            bool firstRun = lastProxyEnabled == null;
            bool changed = proxyEnabled != lastProxyEnabled && !firstRun;
            if (proxyEnabled)
            {
                EnableProxyMenuItem.Visibility = Visibility.Collapsed;
                DisableProxyMenuItem.Visibility = Visibility.Visible;
                ProxyTaskBarIcon.Icon = Properties.Icons.ProxyEnabledSmall;
                message = "Proxy enabled";
            }
            else
            {
                EnableProxyMenuItem.Visibility = Visibility.Visible;
                DisableProxyMenuItem.Visibility = Visibility.Collapsed;
                ProxyTaskBarIcon.Icon = Properties.Icons.ProxyDisabledSmall;
                message = "Proxy disabled";
            }
            ProxyTaskBarIcon.ToolTipText = message;
            if (changed || (firstRun && proxyEnabled))
            {
                // Notify changes and proxy not enabled on first run
                ProxyTaskBarIcon.ShowBalloonTip("Proxy", message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
            }
            lastProxyEnabled = proxyEnabled;
        }

        private const string gitConfig = ".gitconfig";

        private bool IsProxyEnabled
        {
            get
            {
                return Registry.GetValue(registryKeyFullPath, registryValueName, null) is byte[] bytes && bytes.Length > 8 && (bytes[8] & proxyBitMask) != 0;
            }
            set
            {
                if (Registry.GetValue(registryKeyFullPath, registryValueName, null) is byte[] bytes && bytes.Length > 8)
                {
                    bytes[8] = (byte)((bytes[8] & ~proxyBitMask) | (value ? proxyBitMask : 0));
                    Registry.SetValue(registryKeyFullPath, registryValueName, bytes);
                }
                var path = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}{System.IO.Path.DirectorySeparatorChar}{gitConfig}";
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        var text = System.IO.File.ReadAllText(path);
                        string newText;
                        if (value)
                        {
                            newText = System.Text.RegularExpressions.Regex.Replace(text, @"^(\s*);+(\s*proxy\s*=)", "$1$2", System.Text.RegularExpressions.RegexOptions.Multiline);
                        }
                        else
                        {
                            newText = System.Text.RegularExpressions.Regex.Replace(text, @"^(\s*proxy\s*=)", ";$1", System.Text.RegularExpressions.RegexOptions.Multiline);
                        }
                        if (newText != text && !string.IsNullOrWhiteSpace(newText))
                        {
                            System.IO.File.WriteAllText(path, newText);
                        }
                    }
                }
                catch { }
            }
        }

        private void EnableProxyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            IsProxyEnabled = true;
        }

        private void DisableProxyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            IsProxyEnabled = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            watcher?.Stop();
            watcher?.Dispose();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
