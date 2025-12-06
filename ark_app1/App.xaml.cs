using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ark_app1
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window? Window { get; private set; }
        public User? CurrentUser { get; set; } // This will hold the logged-in user's data.

        // Mutex for single instance check
        private static System.Threading.Mutex _mutex = null;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {            
            const string appName = "ArkStock_SingleInstance_Mutex";
            bool createdNew;
            _mutex = new System.Threading.Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // App is already running! Exiting.
                // Note: In a production WinUI 3 app, you might want to redirect activation to the existing instance.
                // For this beta polish, we ensure strict single instance by exiting the new one.
                System.Diagnostics.Debug.WriteLine("Another instance is running. Exiting.");
                // We cannot easily show a dialog here before exiting in OnLaunched without a Window,
                // but we prevent the double open loop.
                Application.Current.Exit();
                return;
            }

            this.Resources.MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());

            Window = new MainWindow();
            Window.Activate();
        }
    }
}
