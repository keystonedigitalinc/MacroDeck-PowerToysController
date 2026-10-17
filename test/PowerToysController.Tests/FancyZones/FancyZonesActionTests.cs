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

	private const string Primary = "{E2D55CC1-5F19-4C36-BB72-12C519ACAEA3}";
	private const string PrimaryShort = "{782F97B6-9A03-48E3-A236-3EC9892CFC88}";
	private const string Stacked = "{7972E5A3-E9A8-4A13-8B77-602EFBB52ED4}";
	private const string Monitor1 = "AOC2702|SERIAL-A|INSTANCE-1";

	private static System.Text.Json.JsonElement Ticked(params string[] layouts) => System.Text.Json.JsonSerializer.SerializeToElement(layouts);

	[Test]
	public async Task Cycle_moves_to_the_layout_after_the_active_one()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		// Monitor 1 starts on Stacked.
		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("mode", "cycle"), ("layouts", Ticked(Primary, Stacked, "columns")), ("monitor", Monitor1)));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Cli.Calls.Single(), Is.EqualTo("set-layout columns --monitor 1"));
	}

	[Test]
	public async Task Cycle_wraps_from_the_last_to_the_first()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var parameters = P(("mode", "cycle"), ("layouts", Ticked(Primary, Stacked)), ("monitor", Monitor1));
		Assert.That((await harness.Actions.ExecuteAsync("apply-layout", parameters)).Succeeded, Is.True);
		Assert.That((await harness.Actions.ExecuteAsync("apply-layout", parameters)).Succeeded, Is.True);

		Assert.That(rig.Cli.Calls, Is.EqualTo(new[] { $"set-layout {Primary} --monitor 1", $"set-layout {Stacked} --monitor 1" }));
	}

	[Test]
	public async Task Cycle_starts_at_the_first_when_the_active_layout_is_not_ticked()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("mode", "cycle"), ("layouts", Ticked("rows", PrimaryShort)), ("monitor", Monitor1)));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Cli.Calls.Single(), Is.EqualTo("set-layout rows --monitor 1"));
	}

	[Test]
	public async Task Cycle_skips_a_ticked_layout_that_no_longer_exists()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("mode", "cycle"), ("layouts", Ticked(Stacked, "{DELETED0-0000-0000-0000-000000000000}", Primary)), ("monitor", Monitor1)));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Cli.Calls.Single(), Is.EqualTo($"set-layout {Primary} --monitor 1"));
	}

	[Test]
	public async Task Cycle_with_only_deleted_layouts_is_NotFound()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("mode", "cycle"), ("layouts", Ticked("{DELETED0-0000-0000-0000-000000000000}")), ("monitor", Monitor1)));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.NotFound));
		Assert.That(rig.Cli.Calls, Is.Empty);
	}

	[Test]
	public async Task Cycle_with_nothing_ticked_is_an_invalid_parameter()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("mode", "cycle"), ("monitor", Monitor1)));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.InvalidParameter));
		Assert.That(rig.Cli.Calls, Is.Empty);
	}

	[Test]
	public async Task Cycle_on_all_monitors_follows_monitor_1()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("mode", "cycle"), ("layouts", Ticked(Stacked, Primary)), ("monitor", "all")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Cli.Calls.Single(), Is.EqualTo($"set-layout {Primary} --all"));
	}

	[Test]
	public async Task Cycle_list_offers_the_same_layouts()
	{
		var (harness, _) = await ReadyAsync();
		await using var _h = harness;

		var single = OptionValues(await harness.Actions.GetOptionsAsync("apply-layout", "layout", null, P()));
		var list = OptionValues(await harness.Actions.GetOptionsAsync("apply-layout", "layouts", null, P()));

		Assert.That(list, Is.EqualTo(single));
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
