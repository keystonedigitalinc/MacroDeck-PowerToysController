using KeystoneDigital.PowerToysController.Workspaces;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;

namespace KeystoneDigital.PowerToysController.Actions;

internal sealed class LaunchWorkspaceAction(PowerToysComponents components) : IDynamicOptionsActionDefinition
{
	private const string WorkspaceParameter = "workspace";

	public string Id => "launch-workspace";

	public LocalizedText Name => Strings.Actions.LaunchWorkspace.Name();

	public LocalizedText Description => Strings.Actions.LaunchWorkspace.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice(WorkspaceParameter, label: Strings.Actions.LaunchWorkspace.Workspace.Label(), required: true),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IActionExecutor CreateExecutor() => new Executor(components);

	// Workspace names are the user's own content, so they travel as literal labels.
	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var workspaces = components.Workspaces.List();
		return Task.FromResult(new DynamicOptionsResult
		{
			Options = [.. workspaces.Select(w => new ActionParameterOption { Value = w.Id, Label = w.Name })],
			Error = workspaces.Count == 0 ? Strings.Actions.LaunchWorkspace.NoneSaved() : null,
		});
	}

	private sealed class Executor(PowerToysComponents components) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (Results.Gate(components.State.Current) is { } blocked) return Task.FromResult(blocked);

			if (ParameterReader.String(context.Parameters, WorkspaceParameter) is not { } id)
			{
				return Task.FromResult(Results.Invalid(Strings.Actions.LaunchWorkspace.Workspace.Label()));
			}

			return Task.FromResult(components.Workspaces.Launch(id) switch
			{
				WorkspaceOutcome.Launched => ActionResult.Success(),
				WorkspaceOutcome.NotFound => ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.NotFound()),
				_ => ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.NotApplied()),
			});
		}
	}
}
