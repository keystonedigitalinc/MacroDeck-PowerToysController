using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.Theme;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;

namespace KeystoneDigital.PowerToysController.Actions;

internal sealed class ThemeAction(PowerToysComponents components) : IActionDefinition
{
	private const string ModeParameter = "mode";

	public string Id => "theme";

	public LocalizedText Name => Strings.Actions.Theme.Name();

	public LocalizedText Description => Strings.Actions.Theme.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice(
			ModeParameter,
			[
				Results.Option("toggle", Strings.Actions.Theme.Mode.Toggle()),
				Results.Option("dark", Strings.Actions.Theme.Mode.Dark()),
				Results.Option("light", Strings.Actions.Theme.Mode.Light()),
			],
			label: Strings.Actions.Theme.Mode.Label(),
			defaultValue: "toggle",
			required: true),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IActionExecutor CreateExecutor() => new Executor(components);

	private sealed class Executor(PowerToysComponents components) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (Results.Gate(components.State.Current) is { } blocked) return blocked;

			var mode = ParameterReader.String(context.Parameters, ModeParameter) ?? "toggle";
			if (mode is not ("toggle" or "dark" or "light")) return Results.Invalid(Strings.Actions.Theme.Mode.Label());

			return await components.Theme.ApplyAsync(mode, context.CancellationToken).ConfigureAwait(false) switch
			{
				ThemeOutcome.Applied or ThemeOutcome.NoChange => ActionResult.Success(),
				ThemeOutcome.Denied => ActionResult.Failed(ActionErrorCodes.PermissionDenied, Strings.Errors.Elevated()),
				ThemeOutcome.NotRunning => ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.ModuleOff("Light Switch")),
				_ => Results.NotApplied(),
			};
		}
	}
}

internal sealed class ModuleAction(PowerToysComponents components) : IDynamicOptionsActionDefinition
{
	private const string ModuleParameter = "module";
	private const string StateParameter = "state";

	public string Id => "module";

	public LocalizedText Name => Strings.Actions.Module.Name();

	public LocalizedText Description => Strings.Actions.Module.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice(ModuleParameter, label: Strings.Actions.Module.Target.Label(), required: true),
		ActionParameter.Choice(
			StateParameter,
			[
				Results.Option("toggle", Strings.Actions.Module.State.Toggle()),
				Results.Option("on", Strings.Actions.Module.State.On()),
				Results.Option("off", Strings.Actions.Module.State.Off()),
			],
			label: Strings.Actions.Module.State.Label(),
			defaultValue: "toggle",
			required: true),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IActionExecutor CreateExecutor() => new Executor(components);

	// Module keys are PowerToys' own identifiers (content, not UI text), so they travel as literal labels.
	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken) =>
		Task.FromResult(new DynamicOptionsResult
		{
			Options = [.. components.State.Current.Enabled.Keys.Order(StringComparer.OrdinalIgnoreCase).Select(key => new ActionParameterOption { Value = key, Label = key })],
		});

	private sealed class Executor(PowerToysComponents components) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var snapshot = components.State.Current;
			if (snapshot.Status == "not_installed" || snapshot.Status == "elevated" || snapshot.Status == "restarting")
			{
				return Results.Gate(snapshot)!;
			}

			if (ParameterReader.String(context.Parameters, ModuleParameter) is not { } module)
			{
				return Results.Invalid(Strings.Actions.Module.Target.Label());
			}

			var state = ParameterReader.String(context.Parameters, StateParameter) ?? "toggle";
			if (state is not ("toggle" or "on" or "off")) return Results.Invalid(Strings.Actions.Module.State.Label());

			var outcome = await components.Modules.SetAsync(module, state, context.CancellationToken).ConfigureAwait(false);
			await components.State.RefreshAsync(context.CancellationToken).ConfigureAwait(false);

			return outcome switch
			{
				ModuleOutcome.Applied or ModuleOutcome.NoChange => ActionResult.Success(),
				ModuleOutcome.NotFound => ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.NotFound()),
				ModuleOutcome.PolicyForced => ActionResult.Failed(ActionErrorCodes.ProviderRejected, Strings.Errors.PolicyForced(module)),
				ModuleOutcome.NotInstalled => ActionResult.Failed(ActionErrorCodes.NotConfigured, Strings.Errors.NotInstalled()),
				_ => Results.NotApplied(),
			};
		}
	}
}
