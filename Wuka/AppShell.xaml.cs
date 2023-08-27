namespace Wuka
{
    public partial class AppShell : Shell
    {
        protected override void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetNavBarIsVisible(this, false);
        }

        public AppShell()
        {
            InitializeComponent();
        }
    }
}