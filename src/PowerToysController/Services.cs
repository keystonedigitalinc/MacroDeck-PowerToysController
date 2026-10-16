using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.Processes;
using KeystoneDigital.PowerToysController.State;
using KeystoneDigital.PowerToysController.Theme;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace KeystoneDigital.PowerToysController;

internal static class Services
{
	/// <summary>The real machine: PowerToys' install, data files, named events and processes.</summary>
	public static IServiceCollection AddPowerToysServices(this IServiceCollection services)
	{
		services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger>();
			var files = new DiskFileStore();
			var processes = new WindowsPowerToysProcesses();
			var paths = PowerToysLocator.FindInstalled() ?? new PowerToysPaths("", PowerToysPaths.DefaultDataRoot);
			var locator = new PowerToysLocator(processes, PowerToysLocator.FindInstalled, path => SafeRead(files, path));
			var awake = new AwakeControl(files, paths, TimeProvider.System);
			var theme = new ThemeControl(new EventTrigger(new WindowsNamedEventOpener()), new RegistryThemeReader(), TimeProvider.System);
			var modules = new ModuleToggle(files, PowerToysLocator.FindInstalled, processes, new WindowsRunnerControl(new WmiProcessStarter()),
				PowerToysLocator.IsPolicyForced, TimeProvider.System);
			var state = new StateService(locator, awake, theme, modules, logger);
			return new PowerToysComponents(locator, state, new EventTrigger(new WindowsNamedEventOpener()), awake, theme, modules);
		});
		services.AddHostedService(sp => sp.GetRequiredService<PowerToysComponents>().State);
		return services;
	}

	/// <summary>Pre-built components, for tests.</summary>
	public static IServiceCollection AddPowerToysComponents(this IServiceCollection services, PowerToysComponents components) =>
		services.AddSingleton(components);

	private static string? SafeRead(DiskFileStore files, string path)
	{
		try
		{
			return files.Read(path);
		}
		catch (IOException)
		{
			return null;
		}
	}
}
