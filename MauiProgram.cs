using BlazorDatasheet.Extensions;
using Microsoft.Extensions.Logging;
using Parquet.Schema;
using PersonalDataWarehouse.Services;
using PersonalDataWarehouse.Models;
using Radzen;
using PersonalDataWarehouse.Model;
using PersonalDataWarehouse.AI;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Blazored.LocalStorage;

namespace PersonalDataWarehouse
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

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
            // Add services to the container.
            AppMetadata appMetadata = new AppMetadata() { Version = "01.05.00" };
            builder.Services.AddSingleton(appMetadata);

            // Blazor Datasheet
            builder.Services.AddBlazorDatasheet();

            // Blazored Local Storage
            builder.Services.AddBlazoredLocalStorage();

            // Services
            builder.Services.AddSingleton<DataService>();
            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddSingleton<LogService>();
            builder.Services.AddSingleton<OrchestratorMethods>();
            builder.Services.AddSingleton<DatabaseService>();

            // This is required by Excel service to parse strings in binary BIFF2-5 Excel documents
            // encoded with DOS-era code pages.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            builder.Services.AddRadzenComponents();

            // Data Directories
            String folderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\PersonalDataWarehouse";

            // Create folderPath folder if it does not exist
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // PersonalDataWarehouseLog.csv
            string PersonalDataWarehouseLogFilePath = Path.Combine(folderPath, "PersonalDataWarehouseLog.csv");
            if (!File.Exists(PersonalDataWarehouseLogFilePath))
            {
                using (var streamWriter = new StreamWriter(PersonalDataWarehouseLogFilePath))
                {
                    streamWriter.WriteLine($"{DateTime.Now.ToShortDateString()}-{DateTime.Now.ToShortTimeString()} : Log Created");
                }
            }

            // Create Default Database if it doen't exist
            DatabaseService databaseService = new DatabaseService();
            databaseService.CreateDatabase("Default");

            return builder.Build();
        }
    }
}