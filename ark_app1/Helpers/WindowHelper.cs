using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT;

namespace ark_app1.Helpers
{
    public static class WindowHelper
    {
        public static void SetDefaultIcon(Window window)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.SetIcon("Assets/GalleryIcon.ico");
            }
            catch { }
        }
    }
}
