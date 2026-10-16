using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.Locator;
using MacroDeck.Plugin.Testing;
using MacroDeck.Sdk.Actions;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Actions;

[TestFixture]
public sealed partial class Phase1ActionTests
{
	internal static Dictionary<string, object?> P(params (string Key, object? Value)[] values) => values.ToDictionary(v => v.Key, v => v.Value);

	internal static string? ErrorCode(CapabilityInvocationOutcome outcome) =>
		outcome.Error?.Code
		?? (outcome.Data is { ValueKind: JsonValueKind.Object } data && data.TryGetProperty("errorCode", out var code) ? code.GetString() : null);

	internal static async Task<(PluginTestHarness Harness, TestRig Rig)> ReadyAsync(Action<TestRig>? arrange = null)
	{
		var rig = new TestRig();
		arrange?.Invoke(rig);
		await rig.Components.State.RefreshAsync(default);
		var harness = IntegrationTests.CreateHarness(rig);
		await harness.InitializeIntegrationsAsync();
		return (harness, rig);
	}

	[Test]
	public async Task Launch_color_picker_sets_its_event()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("launch-tool", P(("tool", "ColorPicker")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Events.Opened, Is.EqualTo(new[] { PowerToysEvents.NameOf(PowerToysTool.ColorPicker) }));
	}

	[Test]
	public async Task Launch_with_module_off_is_Unavailable_and_sets_nothing()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("launch-tool", P(("tool", "MouseJump")));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.Unavailable));
		Assert.That(rig.Events.Opened, Is.Empty);
		Assert.That(outcome.Error?.Message + outcome.Data?.GetRawText(), Does.Contain("Mouse Jump"));
	}

	[Test]
	public async Task Launch_when_elevated_is_PermissionDenied()
	{
		var (harness, rig) = await ReadyAsync(r => r.Runner.Elevated = true);
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("launch-tool", P(("tool", "ColorPicker")));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.PermissionDenied));
		Assert.That(rig.Events.Opened, Is.Empty);
	}

	[Test]
	public async Task Launch_when_PowerToys_is_closed_is_NotConnected()
	{
		var (harness, _) = await ReadyAsync(r => r.Runner.Running = false);
		await using var _h = harness;

		Assert.That(ErrorCode(await harness.Actions.ExecuteAsync("launch-tool", P(("tool", "ColorPicker")))), Is.EqualTo(ActionErrorCodes.NotConnected));
	}

	[Test]
	public async Task Launch_when_listener_is_missing_is_Unavailable()
	{
		var (harness, rig) = await ReadyAsync(r => r.Events.Results[PowerToysEvents.NameOf(PowerToysTool.ColorPicker)] = TriggerResult.NotRunning);
		await using var _h = harness;

		Assert.That(ErrorCode(await harness.Actions.ExecuteAsync("launch-tool", P(("tool", "ColorPicker")))), Is.EqualTo(ActionErrorCodes.Unavailable));
	}

	[Test]
	public async Task Awake_timed_45_confirms()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("awake", P(("mode", "timed"), ("minutes", 45), ("display-on", true)));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		var properties = JsonNode.Parse(rig.Files.Files[TestRig.Paths.AwakeSettings])!["properties"]!;
		Assert.That(properties["mode"]!.GetValue<int>(), Is.EqualTo(2));
		Assert.That(properties["intervalMinutes"]!.GetValue<int>(), Is.EqualTo(45));
		Assert.That(properties["keepDisplayOn"]!.GetValue<bool>(), Is.True);
	}

	[Test]
	public async Task Awake_until_with_a_bad_time_is_InvalidParameter()
	{
		var (harness, _) = await ReadyAsync();
		await using var _h = harness;

		Assert.That(ErrorCode(await harness.Actions.ExecuteAsync("awake", P(("mode", "until"), ("until", "25:99")))), Is.EqualTo(ActionErrorCodes.InvalidParameter));
	}

	[Test]
	public async Task Theme_dark_when_light_succeeds()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("theme", P(("mode", "dark")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Theme.Value, Is.EqualTo(KeystoneDigital.PowerToysController.Theme.Theme.Dark));
	}

	[Test]
	public async Task Module_off_reports_restart_outcome()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("module", P(("module", "ColorPicker"), ("state", "off")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(EnableMap.Read(rig.Files.Files[TestRig.Paths.GeneralSettings])["ColorPicker"], Is.False);
		Assert.That(rig.Runner.Starts, Is.EqualTo(1));
	}

	[Test]
	public async Task Module_options_list_the_enable_map()
	{
		var (harness, _) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.GetOptionsAsync("module", "module", null, P());

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		var values = outcome.Data!.Value.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("value").GetString()).ToList();
		Assert.That(values, Does.Contain("ColorPicker").And.Contain("Measure Tool"));
	}

	[Test]
	public void Every_action_id_is_a_valid_local_id()
	{
		var integration = PowerToysIntegration.Create(new SingleServiceProvider(new TestRig().Components));

		Assert.That(integration.Actions, Is.Not.Empty);
		foreach (var action in integration.Actions) Assert.That(LocalIdPattern().IsMatch(action.Id), Is.True, action.Id);
	}

	[GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")]
	private static partial Regex LocalIdPattern();

	private sealed class SingleServiceProvider(PowerToysComponents components) : IServiceProvider
	{
		public object? GetService(Type serviceType) => serviceType == typeof(PowerToysComponents) ? components : null;
	}
}
