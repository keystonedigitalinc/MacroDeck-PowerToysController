using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.FancyZones;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;

namespace KeystoneDigital.PowerToysController.Actions;

internal sealed class ApplyLayoutAction(PowerToysComponents components) : IDynamicOptionsActionDefinition
{
	private const string ModeParameter = "mode";
	private const string LayoutParameter = "layout";
	private const string LayoutsParameter = "layouts";
	private const string MonitorParameter = "monitor";
	private const string AllMonitors = "all";

	public string Id => "apply-layout";

	public LocalizedText Name => Strings.Actions.ApplyLayout.Name();

	public LocalizedText Description => Strings.Actions.ApplyLayout.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice(
			ModeParameter,
			[
				Results.Option("layout", Strings.Actions.ApplyLayout.Mode.Layout()),
				Results.Option("cycle", Strings.Actions.ApplyLayout.Mode.Cycle()),
			],
			label: Strings.Actions.ApplyLayout.Mode.Label(),
			defaultValue: "layout",
			required: true),
		ActionParameter.DynamicChoice(LayoutParameter, label: Strings.Actions.ApplyLayout.Layout.Label())
			.OnlyWhen(ModeParameter, "layout"),
		LayoutList(),
		ActionParameter.DynamicChoice(MonitorParameter, label: Strings.Actions.ApplyLayout.Monitor.Label(), required: true),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	// No optionsSourceId: the host reads that as the name of one of its own sources. With no static
	// options and DynamicOptions set, the host asks GetDynamicOptionsAsync instead.
	private static ActionParameter LayoutList()
	{
		var parameter = new ActionParameter
		{
			Name = LayoutsParameter,
			Type = ActionParameterType.MultiSelect,
			Label = Strings.Actions.ApplyLayout.Layouts.Label(),
			Options = [],
			DynamicOptions = true,
		};
		return parameter.OnlyWhen(ModeParameter, "cycle");
	}

	public IActionExecutor CreateExecutor() => new Executor(components);

	// Layout and monitor names are content from FancyZones, so they travel as literal labels.
	public async Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		if (context.ParameterName is LayoutParameter or LayoutsParameter)
		{
			return new DynamicOptionsResult
			{
				Options = [.. components.FancyZones.Layouts().Select(layout => new ActionParameterOption { Value = layout.Id, Label = layout.Name })],
			};
		}

		var options = new List<ActionParameterOption> { Results.Option(AllMonitors, Strings.Actions.ApplyLayout.AllMonitors()) };
		var monitors = await components.FancyZones.MonitorsAsync(cancellationToken).ConfigureAwait(false);
		options.AddRange(monitors.Select(m => new ActionParameterOption { Value = m.Key, Label = $"{m.Index}: {m.Monitor}" }));

		// A saved monitor that is unplugged stays visible instead of silently blanking the field.
		var current = ParameterReader.String(context.CurrentParameters ?? new Dictionary<string, object?>(), MonitorParameter);
		if (current is not null && options.All(option => option.Value != current))
		{
			options.Add(Results.Option(current, Strings.Actions.ApplyLayout.MonitorMissing(current.Split('|')[0])));
		}

		return new DynamicOptionsResult { Options = options };
	}

	private sealed class Executor(PowerToysComponents components) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var snapshot = components.State.Current;
			if (Results.Gate(snapshot) is { } blocked) return blocked;
			if (snapshot.Enabled.TryGetValue("FancyZones", out var on) && !on)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.ModuleOff("FancyZones"));
			}

			// Buttons saved before cycling existed have no mode: they apply one layout.
			var cycling = ParameterReader.String(context.Parameters, ModeParameter) == "cycle";
			var layout = ParameterReader.String(context.Parameters, LayoutParameter);
			var cycle = ParameterReader.StringList(context.Parameters, LayoutsParameter);
			if (!cycling && layout is null) return Results.Invalid(Strings.Actions.ApplyLayout.Layout.Label());
			if (cycling && cycle.Count == 0) return Results.Invalid(Strings.Actions.ApplyLayout.Layouts.Label());
			if (ParameterReader.String(context.Parameters, MonitorParameter) is not { } monitor) return Results.Invalid(Strings.Actions.ApplyLayout.Monitor.Label());

			var monitorKey = monitor == AllMonitors ? null : monitor;
			var outcome = cycling
				? await components.FancyZones.CycleAsync(cycle, monitorKey, context.CancellationToken).ConfigureAwait(false)
				: await components.FancyZones.ApplyAsync(layout!, monitorKey, context.CancellationToken).ConfigureAwait(false);
			await components.State.RefreshAsync(context.CancellationToken).ConfigureAwait(false);

			return outcome switch
			{
				LayoutOutcome.Applied => ActionResult.Success(),
				LayoutOutcome.NotFound => ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.NotFound()),
				LayoutOutcome.CliFailed => ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.CliFailed(components.FancyZones.LastCliOutput?.Trim() ?? "")),
				LayoutOutcome.NotRunning => ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.ModuleOff("FancyZones")),
				LayoutOutcome.NotInstalled => ActionResult.Failed(ActionErrorCodes.NotConfigured, Strings.Errors.NotInstalled()),
				_ => Results.NotApplied(),
			};
		}
	}
}

internal sealed class OpenZoneEditorAction(PowerToysComponents components) : IActionDefinition
{
	public string Id => "open-zone-editor";

	public LocalizedText Name => Strings.Actions.OpenZoneEditor.Name();

	public LocalizedText Description => Strings.Actions.OpenZoneEditor.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IActionExecutor CreateExecutor() => new Executor(components);

	private sealed class Executor(PowerToysComponents components) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (Results.Gate(components.State.Current) is { } blocked) return Task.FromResult(blocked);

			return Task.FromResult(components.Trigger.Trigger(PowerToysTool.FancyZonesEditor) switch
			{
				TriggerResult.Triggered => ActionResult.Success(),
				TriggerResult.Denied => ActionResult.Failed(ActionErrorCodes.PermissionDenied, Strings.Errors.Elevated()),
				_ => ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.ModuleOff("FancyZones")),
			});
		}
	}
}
