using KeystoneDigital.PowerToysController.Events;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;

namespace KeystoneDigital.PowerToysController.Actions;

internal sealed class LaunchToolAction(PowerToysComponents components) : IActionDefinition
{
	private const string ToolParameter = "tool";

	internal static readonly IReadOnlyList<(PowerToysTool Tool, Func<LocalizedText> Name)> Tools =
	[
		(PowerToysTool.ColorPicker, () => Strings.Actions.LaunchTool.Tools.ColorPicker()),
		(PowerToysTool.TextExtractor, () => Strings.Actions.LaunchTool.Tools.TextExtractor()),
		(PowerToysTool.ScreenRuler, () => Strings.Actions.LaunchTool.Tools.ScreenRuler()),
		(PowerToysTool.ShortcutGuide, () => Strings.Actions.LaunchTool.Tools.ShortcutGuide()),
		(PowerToysTool.CommandPalette, () => Strings.Actions.LaunchTool.Tools.CommandPalette()),
		(PowerToysTool.AlwaysOnTop, () => Strings.Actions.LaunchTool.Tools.AlwaysOnTop()),
		(PowerToysTool.CropAndLockReparent, () => Strings.Actions.LaunchTool.Tools.CropAndLockReparent()),
		(PowerToysTool.CropAndLockThumbnail, () => Strings.Actions.LaunchTool.Tools.CropAndLockThumbnail()),
		(PowerToysTool.CropAndLockScreenshot, () => Strings.Actions.LaunchTool.Tools.CropAndLockScreenshot()),
		(PowerToysTool.FindMyMouse, () => Strings.Actions.LaunchTool.Tools.FindMyMouse()),
		(PowerToysTool.MouseHighlighter, () => Strings.Actions.LaunchTool.Tools.MouseHighlighter()),
		(PowerToysTool.MouseCrosshairs, () => Strings.Actions.LaunchTool.Tools.MouseCrosshairs()),
		(PowerToysTool.MouseJump, () => Strings.Actions.LaunchTool.Tools.MouseJump()),
		(PowerToysTool.CursorWrap, () => Strings.Actions.LaunchTool.Tools.CursorWrap()),
		(PowerToysTool.AdvancedPaste, () => Strings.Actions.LaunchTool.Tools.AdvancedPaste()),
		(PowerToysTool.Hosts, () => Strings.Actions.LaunchTool.Tools.Hosts()),
		(PowerToysTool.HostsAdmin, () => Strings.Actions.LaunchTool.Tools.HostsAdmin()),
		(PowerToysTool.EnvironmentVariables, () => Strings.Actions.LaunchTool.Tools.EnvironmentVariables()),
		(PowerToysTool.EnvironmentVariablesAdmin, () => Strings.Actions.LaunchTool.Tools.EnvironmentVariablesAdmin()),
		(PowerToysTool.RegistryPreview, () => Strings.Actions.LaunchTool.Tools.RegistryPreview()),
		(PowerToysTool.KeyboardManagerEditor, () => Strings.Actions.LaunchTool.Tools.KeyboardManagerEditor()),
		(PowerToysTool.PowerDisplay, () => Strings.Actions.LaunchTool.Tools.PowerDisplay()),
	];

	public string Id => "launch-tool";

	public LocalizedText Name => Strings.Actions.LaunchTool.Name();

	public LocalizedText Description => Strings.Actions.LaunchTool.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice(
			ToolParameter,
			[.. Tools.Select(tool => Results.Option(tool.Tool.ToString(), tool.Name()))],
			label: Strings.Actions.LaunchTool.Tool.Label(),
			defaultValue: nameof(PowerToysTool.ColorPicker),
			required: true),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IActionExecutor CreateExecutor() => new ToolExecutor(components, Tools, ToolParameter, () => Strings.Actions.LaunchTool.Tool.Label());
}

/// <summary>
/// One PowerToys module's own tools as one action, such as ZoomIt's Draw or Snip, so a deck can trigger
/// them without the module's keyboard shortcuts.
/// </summary>
internal sealed class ModuleToolAction(
	PowerToysComponents components,
	string id,
	Func<LocalizedText> name,
	Func<LocalizedText> description,
	IReadOnlyList<(PowerToysTool Tool, Func<LocalizedText> Name)> tools) : IActionDefinition
{
	private const string CommandParameter = "command";

	public static IReadOnlyList<ModuleToolAction> All(PowerToysComponents components) =>
	[
		new(components, "zoomit", () => Strings.Actions.ZoomIt.Name(), () => Strings.Actions.ZoomIt.Description(),
		[
			(PowerToysTool.ZoomItZoom, () => Strings.Actions.ZoomIt.Commands.Zoom()),
			(PowerToysTool.ZoomItDraw, () => Strings.Actions.ZoomIt.Commands.Draw()),
			(PowerToysTool.ZoomItLiveZoom, () => Strings.Actions.ZoomIt.Commands.LiveZoom()),
			(PowerToysTool.ZoomItSnip, () => Strings.Actions.ZoomIt.Commands.Snip()),
			(PowerToysTool.ZoomItSnipOcr, () => Strings.Actions.ZoomIt.Commands.SnipOcr()),
			(PowerToysTool.ZoomItSnipDictate, () => Strings.Actions.ZoomIt.Commands.SnipDictate()),
			(PowerToysTool.ZoomItRecord, () => Strings.Actions.ZoomIt.Commands.Record()),
			(PowerToysTool.ZoomItBreak, () => Strings.Actions.ZoomIt.Commands.Break()),
		]),
		new(components, "always-on-top", () => Strings.Actions.AlwaysOnTop.Name(), () => Strings.Actions.AlwaysOnTop.Description(),
		[
			(PowerToysTool.AlwaysOnTop, () => Strings.Actions.AlwaysOnTop.Commands.Pin()),
			(PowerToysTool.AlwaysOnTopOpacityUp, () => Strings.Actions.AlwaysOnTop.Commands.OpacityUp()),
			(PowerToysTool.AlwaysOnTopOpacityDown, () => Strings.Actions.AlwaysOnTop.Commands.OpacityDown()),
		]),
		new(components, "mouse-without-borders", () => Strings.Actions.MouseWithoutBorders.Name(), () => Strings.Actions.MouseWithoutBorders.Description(),
		[
			(PowerToysTool.MouseWithoutBordersEasyMouse, () => Strings.Actions.MouseWithoutBorders.Commands.EasyMouse()),
			(PowerToysTool.MouseWithoutBordersReconnect, () => Strings.Actions.MouseWithoutBorders.Commands.Reconnect()),
		]),
	];

	public string Id => id;

	public LocalizedText Name => name();

	public LocalizedText Description => description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice(
			CommandParameter,
			[.. tools.Select(tool => Results.Option(tool.Tool.ToString(), tool.Name()))],
			label: Strings.Actions.ModuleTool.Command.Label(),
			defaultValue: tools[0].Tool.ToString(),
			required: true),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IActionExecutor CreateExecutor() => new ToolExecutor(components, tools, CommandParameter, () => Strings.Actions.ModuleTool.Command.Label());
}

/// <summary>Sets the named event of the chosen tool, after checking that PowerToys and its module are up.</summary>
internal sealed class ToolExecutor(
	PowerToysComponents components,
	IReadOnlyList<(PowerToysTool Tool, Func<LocalizedText> Name)> tools,
	string parameter,
	Func<LocalizedText> label) : IActionExecutor
{
	public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
	{
		var snapshot = components.State.Current;
		if (Results.Gate(snapshot) is { } blocked) return Task.FromResult(blocked);

		if (!Enum.TryParse<PowerToysTool>(ParameterReader.String(context.Parameters, parameter), out var tool)
			|| tools.All(t => t.Tool != tool))
		{
			return Task.FromResult(Results.Invalid(label()));
		}

		var name = PowerToysEvents.ProductName(tool);
		if (snapshot.Enabled.TryGetValue(PowerToysEvents.ModuleOf(tool), out var on) && !on)
		{
			return Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.ModuleOff(name)));
		}

		return Task.FromResult(components.Trigger.Trigger(tool) switch
		{
			TriggerResult.Triggered => ActionResult.Success(),
			TriggerResult.Denied => ActionResult.Failed(ActionErrorCodes.PermissionDenied, Strings.Errors.Elevated()),
			_ => ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.ModuleOff(name)),
		});
	}
}
