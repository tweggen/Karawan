using Microsoft.Maui.Controls;
namespace Wuka
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
    }
}