using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace WPFModelViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //Settings, logs, shaders and other data files are opened with relative paths.
            //Pin the working directory to the exe folder so they are found no matter how
            //the app was launched (shortcut, file explorer, another folder, Visual Studio)
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            base.OnStartup(e);

            SetupExceptionHandling();
        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            DispatcherUnhandledException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
                Util.showError("An unhandled exception occurred: " + e.Exception.Message, "Error");
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                message = string.Format("Unhandled exception in {0} v{1}", assemblyName.Name, assemblyName.Version);
            }
            catch (Exception ex)
            {
                Util.Log("{0}", "Exception in LogUnhandledException: " + ex);
            }
            finally
            {
                //Same log.out as the rest of the app (NLog had no configuration, so these went nowhere)
                Util.Log("{0}", message + ": " + exception);
            }
        }
    }
}
