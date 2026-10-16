using Microsoft.Extensions.DependencyInjection;

namespace KeystoneDigital.PowerToysController;

internal static class Services
{
	/// <summary>The real machine: PowerToys' install, data files, named events and processes.</summary>
	public static IServiceCollection AddPowerToysServices(this IServiceCollection services) => services;
}
