using PalSearch.Model;
using PalSearch.UI.Localization;
using Serilog;
using System;
using System.IO;
using System.Windows;

namespace PalSearch.UI
{
    public partial class App : Application
    {
        public static string Version => "v1.0.0";
        public static string LogFolder = "log";

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                Log.Fatal(ex.ExceptionObject as Exception, "Unhandled error");
                Log.CloseAndFlush();
            };

            if (!Directory.Exists(LogFolder)) Directory.CreateDirectory(LogFolder);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File($"{LogFolder}/log.txt", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
                .CreateLogger();

            Log.Information($"PalSearch version {Version}");

            Translator.Init();
            PalDB.BeginLoadEmbedded();

            base.OnStartup(e);
        }
    }
}