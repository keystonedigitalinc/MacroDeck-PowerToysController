using System.Globalization;
using KeystoneDigital.PowerToysController.Awake;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;

namespace KeystoneDigital.PowerToysController.Actions;

internal sealed class AwakeAction(PowerToysComponents components, TimeProvider time) : IActionDefinition
{
	private const string ModeParameter = "mode";
	private const string MinutesParameter = "minutes";
	private const string UntilParameter = "until";
	private const string DisplayParameter = "display-on";

	public string Id => "awake";

	public LocalizedText Name => Strings.Actions.Awake.Name();

	public LocalizedText Description => Strings.Actions.Awake.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice(
			ModeParameter,
			[
				Results.Option("off", Strings.Actions.Awake.Mode.Off()),
				Results.Option("indefinite", Strings.Actions.Awake.Mode.Indefinite()),
				Results.Option("timed", Strings.Actions.Awake.Mode.Timed()),
				Results.Option("until", Strings.Actions.Awake.Mode.Until()),
			],
			label: Strings.Actions.Awake.Mode.Label(),
			defaultValue: "indefinite",
			required: true),
		ActionParameter.Number(MinutesParameter, label: Strings.Actions.Awake.Minutes.Label(), min: 1, max: 1440, step: 1, defaultValue: 30)
			.OnlyWhen(ModeParameter, "timed"),
		ActionParameter.Text(UntilParameter, label: Strings.Actions.Awake.Until.Label(), placeholder: "23:00", validationRegex: "^([01]?[0-9]|2[0-3]):[0-5][0-9]$")
			.OnlyWhen(ModeParameter, "until"),
		ActionParameter.Toggle(DisplayParameter, label: Strings.Actions.Awake.DisplayOn.Label()),
	];

	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IActionExecutor CreateExecutor() => new Executor(components, time);

	/// <summary>The next occurrence of "HH:mm" in local time: today if still ahead, else tomorrow.</summary>
	internal static DateTimeOffset? NextOccurrence(string? text, DateTimeOffset now)
	{
		if (!TimeOnly.TryParseExact(text ?? "", ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) return null;

		var candidate = new DateTimeOffset(now.Date + at.ToTimeSpan(), now.Offset);
		return candidate > now ? candidate : candidate.AddDays(1);
	}

	private sealed class Executor(PowerToysComponents components, TimeProvider time) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var snapshot = components.State.Current;
			if (Results.Gate(snapshot) is { } blocked) return blocked;

			// Awake ignores its settings while the module is off, so writing them would be a false success.
			if (snapshot.Enabled.TryGetValue("Awake", out var on) && !on)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.ModuleOff("Awake"));
			}

			var mode = (ParameterReader.String(context.Parameters, ModeParameter) ?? "indefinite") switch
			{
				"off" => AwakeMode.Off,
				"timed" => AwakeMode.Timed,
				"until" => AwakeMode.Until,
				_ => AwakeMode.Indefinite,
			};
			var minutes = ParameterReader.Int(context.Parameters, MinutesParameter) ?? 30;
			var display = ParameterReader.String(context.Parameters, DisplayParameter) is "true" or "True";

			DateTimeOffset? until = null;
			if (mode == AwakeMode.Until)
			{
				until = NextOccurrence(ParameterReader.String(context.Parameters, UntilParameter), time.GetLocalNow());
				if (until is null) return Results.Invalid(Strings.Actions.Awake.Until.Label());
			}

			var ok = await components.Awake.SetAsync(mode, display, Math.Clamp(minutes, 1, 1440), until, context.CancellationToken).ConfigureAwait(false);
			return ok ? ActionResult.Success() : Results.NotApplied();
		}
	}
}
