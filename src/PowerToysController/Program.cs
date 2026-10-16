using KeystoneDigital.PowerToysController;
using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;

var builder = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.UseLocalization(Strings.LocalizationCatalog);

builder.Services.AddPowerToysServices();

var plugin = builder
	.RegisterIntegration(PowerToysIntegration.Create)
	.Build();

await plugin.RunAsync();
