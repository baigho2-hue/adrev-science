using System;
using System.Linq;
using System.Windows;
using AdRev.Core.Services;
using AdRev.Domain.Models;

namespace AdRev.Desktop
{
    public partial class App : Application
    {
        private readonly LicensingService _licensingService = new LicensingService();

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                LogException(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
            };
        }

        private void LogException(Exception? ex, string source)
        {
            if (ex == null) return;
            string logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            string message = $"{DateTime.Now}: [{source}] {ex.Message}\nStack Trace: {ex.StackTrace}\n\n";
            try { System.IO.File.AppendAllText(logFile, message); } 
            catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Erreur d'écriture log: {logEx.Message}"); }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "DispatcherUnhandledException");
            
            var ex = e.Exception;
            string message = ex.Message;
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
                message += "\n\nInner Exception: " + ex.Message;
            }

            MessageBox.Show($"Une erreur inattendue est survenue : {message}\n\n{ex.StackTrace}", "Erreur Critique", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. Check if License is Valid
                if (!_licensingService.IsActivated(out string _))
                {
                    // Show Welcome Window for first time users
                    var welcomeWindow = new WelcomeWindow();
                    bool? result = welcomeWindow.ShowDialog();

                    if (result != true && !_licensingService.IsActivated(out _))
                    {
                        // User closed activation or it failed
                        Shutdown();
                        return;
                    }
                }

                // 2. License OK, show MainWindow
                MainWindow window = new MainWindow();
                window.Show();
            }
            catch (Exception ex)
            {
                LogException(ex, "OnStartup");
                MessageBox.Show($"Erreur au démarrage: {ex.Message}", "Erreur Fatale");
                Shutdown();
            }
        }

        public void SetLanguage(string cultureCode)
        {
            ResourceDictionary dict = new ResourceDictionary();
            switch (cultureCode)
            {
                case "en":
                    dict.Source = new Uri("Resources/Localization/Strings.en.xaml", UriKind.Relative);
                    break;
                case "de":
                    dict.Source = new Uri("Resources/Localization/Strings.de.xaml", UriKind.Relative);
                    break;
                case "es":
                    dict.Source = new Uri("Resources/Localization/Strings.es.xaml", UriKind.Relative);
                    break;
                default:
                    dict.Source = new Uri("Resources/Localization/Strings.fr.xaml", UriKind.Relative);
                    break;
            }

            var oldDict = Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));
            if (oldDict != null)
            {
                Resources.MergedDictionaries.Remove(oldDict);
            }
            Resources.MergedDictionaries.Add(dict);
        }

        public static string GetString(string key)
        {
            try
            {
                if (Application.Current.Resources.Contains(key))
                {
                    return Application.Current.Resources[key] as string ?? key;
                }
                return key;
            }
            catch
            {
                return key;
            }
        }
    }
}
