using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;

namespace KeystoneDigital.PowerToysController;

public sealed class PowerToysIntegration : IPluginIntegration
{
	internal PowerToysIntegration()
	{
		Actions = [];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	public static PowerToysIntegration Create(IServiceProvider services) => new();

	public Task InitializeAsync(IIntegrationContext context) => Task.CompletedTask;

	public Task ShutdownAsync() => Task.CompletedTask;
}
