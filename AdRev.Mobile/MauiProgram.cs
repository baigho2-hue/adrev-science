using Microsoft.Extensions.Logging;
using BarcodeScanner.Mobile;

namespace AdRev.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddBarcodeScannerHandler();
            })
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
        
        // Register Services
            builder.Services.AddSingleton<Services.DatabaseService>();
            builder.Services.AddSingleton<Services.ApiClient>();
            builder.Services.AddSingleton<Services.ImportService>();
            builder.Services.AddSingleton<Services.BillingService>();

            builder.Services.AddSingleton<DashboardPage>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<FormsPage>();

		return builder.Build();
	}
}
