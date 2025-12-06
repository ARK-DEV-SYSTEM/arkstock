using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ark_app1
{
    public sealed partial class AboutArkDevPage : Page
    {
        public AboutArkDevPage()
        {
            this.InitializeComponent();
        }

        private async void ContactButton_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://cal.com/ark-deven"));
        }
    }
}