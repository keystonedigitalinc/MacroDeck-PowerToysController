using System.Text.RegularExpressions;
using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.State;
using MacroDeck.Sdk.Variables;

namespace KeystoneDigital.PowerToysController.Variables;

/// <summary>
/// The plugin's variables. Names are persisted in users' widgets, so they are a public API. Values are
/// stable English tokens that widgets compare against, never UI text.
/// </summary>
internal static partial class PowerToysVariables
{
	public const int MonitorCount = 4;

	public static readonly IReadOnlyList<string> Modules =
	[
		"AlwaysOnTop", "Awake", "CmdPal", "ColorPicker", "CropAndLock", "FancyZones", "FindMyMouse", "LightSwitch",
		"Measure Tool", "MouseHighlighter", "MouseJump", "MousePointerCrosshairs", "Peek", "Shortcut Guide",
		"TextExtractor", "Workspaces",
	];

	private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(2);

	public static IReadOnlyList<VariableDefinition> Definitions { get; } =
	[
		Text("powertoys_status", "status"),
		Text("powertoys_version", "version"),
		Text("powertoys_awake_mode", "awake-mode"),
		VariableDefinition.Eager("powertoys_awake_display_on", VariableType.Boolean, refreshInterval: Refresh) with { Id = "awake-display-on" },
		Text("powertoys_theme", "theme"),
		.. Modules.Select(module =>
			VariableDefinition.Eager($"powertoys_{Slug(module)}_enabled", VariableType.Boolean, refreshInterval: Refresh) with { Id = LocalId(module) }),
		.. Enumerable.Range(1, MonitorCount).Select(n => Text($"powertoys_layout_monitor_{n}", $"layout-monitor-{n}")),
	];

	public static string Slug(string module) => NonAlphanumeric().Replace(module.ToLowerInvariant(), "_");

	public static string LocalId(string module) => "module-" + NonAlphanumeric().Replace(module.ToLowerInvariant(), "-");

	public static VariableReading Read(string localId, PowerToysSnapshot snapshot)
	{
		object? value = localId switch
		{
			"status" => snapshot.Status,
			"version" => snapshot.Version,
			"awake-mode" => snapshot.Awake?.Mode switch
			{
				AwakeMode.Off => "off",
				AwakeMode.Indefinite => "indefinite",
				AwakeMode.Timed => "timed",
				AwakeMode.Until => "until",
				_ => null,
			},
			"awake-display-on" => snapshot.Awake?.KeepDisplayOn,
			"theme" => snapshot.Theme switch
			{
				Theme.Theme.Dark => "dark",
				Theme.Theme.Light => "light",
				_ => null,
			},
			_ when localId.StartsWith("module-", StringComparison.Ordinal) => ModuleValue(localId, snapshot),
			_ when localId.StartsWith("layout-monitor-", StringComparison.Ordinal) => MonitorValue(localId, snapshot),
			_ => null,
		};

		return value is null ? VariableReading.Unavailable : VariableReading.Of(value);
	}

	private static bool? ModuleValue(string localId, PowerToysSnapshot snapshot)
	{
		var module = Modules.FirstOrDefault(m => LocalId(m) == localId);
		return module is not null && snapshot.Enabled.TryGetValue(module, out var on) ? on : (bool?)null;
	}

	private static string? MonitorValue(string localId, PowerToysSnapshot snapshot) =>
		int.TryParse(localId["layout-monitor-".Length..], out var index)
			? snapshot.Monitors.FirstOrDefault(m => m.Index == index)?.ActiveLayoutName
			: null;

	private static VariableDefinition Text(string name, string id) =>
		VariableDefinition.Eager(name, VariableType.Text, refreshInterval: Refresh) with { Id = id };

	[GeneratedRegex("[^a-z0-9]+")]
	private static partial Regex NonAlphanumeric();
}
