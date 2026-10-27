using KeystoneDigital.PowerToysController.Actions;
using KeystoneDigital.PowerToysController.Variables;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using Microsoft.Extensions.DependencyInjection;

namespace KeystoneDigital.PowerToysController;

public sealed class PowerToysIntegration : IPluginIntegration, IVariableProvider
{
	internal PowerToysIntegration(PowerToysComponents components)
	{
		Components = components;
		Actions =
		[
			new LaunchToolAction(components),
			.. ModuleToolAction.All(components),
			new AwakeAction(components, TimeProvider.System),
			new ThemeAction(components),
			new ModuleAction(components),
			new ApplyLayoutAction(components),
			new OpenZoneEditorAction(components),
			new LaunchWorkspaceAction(components),
		];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	public IReadOnlyList<VariableDefinition> Variables => PowerToysVariables.Definitions;

	internal PowerToysComponents Components { get; }

	public static PowerToysIntegration Create(IServiceProvider services) => new(services.GetRequiredService<PowerToysComponents>());

	public Task InitializeAsync(IIntegrationContext context) => Components.State.RefreshAsync(CancellationToken.None);

	public Task ShutdownAsync() => Task.CompletedTask;

	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default) =>
		ValueTask.FromResult(PowerToysVariables.Read(localId, Components.State.Current));
}
