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

	public IActionExecutor CreateExecutor() => new Executor(components);

	private sealed class Executor(PowerToysComponents components) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var snapshot = components.State.Current;
			if (Results.Gate(snapshot) is { } blocked) return Task.FromResult(blocked);

			if (!Enum.TryParse<PowerToysTool>(ParameterReader.String(context.Parameters, ToolParameter), out var tool)
				|| Tools.All(t => t.Tool != tool))
			{
				return Task.FromResult(Results.Invalid(Strings.Actions.LaunchTool.Tool.Label()));
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
}
