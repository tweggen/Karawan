﻿//using Microsoft.Extensions.Logging;

using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace Wuka
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

//#if DEBUG
//		builder.Logging.AddDebug();
//#endif

            return builder.Build();
        }
    }
}