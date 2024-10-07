using System;

namespace MauiDoom
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new DoomPage();
        }
    }
}
