using KeystoneDigital.PowerToysController.Events;
using MacroDeck.Sdk.Actions;
using NUnit.Framework;
using static KeystoneDigital.PowerToysController.Tests.Actions.Phase1ActionTests;

namespace KeystoneDigital.PowerToysController.Tests.FancyZones;

[TestFixture]
public sealed class FancyZonesActionTests
{
	private static readonly string[] MonitorOptions = ["all", "AOC2702|SERIAL-A|INSTANCE-1", "AOC2402|SERIAL-B|INSTANCE-2"];

	private static List<string?> OptionValues(MacroDeck.Plugin.Testing.CapabilityInvocationOutcome outcome)
	{
		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		return [.. outcome.Data!.Value.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("value").GetString())];
	}

	[Test]
	public async Task Apply_layout_to_a_monitor_succeeds()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("layout", "{E2D55CC1-5F19-4C36-BB72-12C519ACAEA3}"), ("monitor", "AOC2702|SERIAL-A")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Cli.Calls.Single(), Is.EqualTo("set-layout {E2D55CC1-5F19-4C36-BB72-12C519ACAEA3} --monitor 1"));
	}

	[Test]
	public async Task Apply_layout_to_a_gone_monitor_is_NotFound()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("layout", "columns"), ("monitor", "GONE|X")));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.NotFound));
		Assert.That(rig.Cli.Calls, Is.Empty);
	}

	[Test]
	public async Task Apply_layout_cli_failure_is_ProviderError()
	{
		var (harness, rig) = await ReadyAsync(r => r.Cli.ExitCode = 2);
		await using var _h = harness;

		Assert.That(ErrorCode(await harness.Actions.ExecuteAsync("apply-layout", P(("layout", "columns"), ("monitor", "all")))), Is.EqualTo(ActionErrorCodes.ProviderError));
	}

	[Test]
	public async Task Layout_and_monitor_options_come_from_FancyZones()
	{
		var (harness, _) = await ReadyAsync();
		await using var _h = harness;

		var layouts = OptionValues(await harness.Actions.GetOptionsAsync("apply-layout", "layout", null, P()));
		var monitors = OptionValues(await harness.Actions.GetOptionsAsync("apply-layout", "monitor", null, P()));

		Assert.That(layouts, Does.Contain("{7972E5A3-E9A8-4A13-8B77-602EFBB52ED4}").And.Contain("columns"));
		Assert.That(monitors, Is.EqualTo(MonitorOptions));
	}

	[Test]
	public async Task Open_zone_editor_sets_its_event()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("open-zone-editor", P());

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Events.Opened.Single(), Is.EqualTo(PowerToysEvents.NameOf(PowerToysTool.FancyZonesEditor)));
	}

	[Test]
	public async Task Monitor_layout_variables_are_live()
	{
		var (harness, _) = await ReadyAsync();
		await using var _h = harness;

		Assert.That(KeystoneDigital.PowerToysController.Tests.State.StateTests.ValueOf(await harness.Variables.GetAsync("layout-monitor-1")), Is.EqualTo("Stacked"));
	}
}
