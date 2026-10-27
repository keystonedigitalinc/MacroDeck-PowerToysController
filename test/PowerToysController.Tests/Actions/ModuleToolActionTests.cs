using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.Locator;
using MacroDeck.Sdk.Actions;
using NUnit.Framework;
using static KeystoneDigital.PowerToysController.Tests.Actions.Phase1ActionTests;

namespace KeystoneDigital.PowerToysController.Tests.Actions;

[TestFixture]
public sealed class ModuleToolActionTests
{
	// The settings fixture has ZoomIt switched off, as on the machine it came from.
	private static void ZoomItOn(TestRig rig) => rig.Files.Files[TestRig.Paths.GeneralSettings] =
		EnableMap.WithModule(rig.Files.Files[TestRig.Paths.GeneralSettings], "ZoomIt", true);

	[Test]
	public async Task ZoomIt_draw_sets_its_event()
	{
		var (harness, rig) = await ReadyAsync(ZoomItOn);
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("zoomit", P(("command", "ZoomItDraw")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Events.Opened.Single(), Is.EqualTo(@"Local\PowerToysZoomIt-DrawEvent-56338997-404d-4549-bd9a-d132b6766975"));
	}

	[Test]
	public async Task ZoomIt_with_the_module_off_is_Unavailable_and_sets_nothing()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("zoomit", P(("command", "ZoomItSnip")));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.Unavailable));
		Assert.That(rig.Events.Opened, Is.Empty);
	}

	[Test]
	public async Task A_command_of_another_module_is_rejected()
	{
		var (harness, rig) = await ReadyAsync(ZoomItOn);
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("zoomit", P(("command", "ColorPicker")));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.InvalidParameter));
		Assert.That(rig.Events.Opened, Is.Empty);
	}

	[Test]
	public async Task Always_on_top_opacity_up_sets_its_event()
	{
		var (harness, rig) = await ReadyAsync();
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("always-on-top", P(("command", "AlwaysOnTopOpacityUp")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Events.Opened, Is.EqualTo(new[] { PowerToysEvents.NameOf(PowerToysTool.AlwaysOnTopOpacityUp) }));
	}

	[Test]
	public void Every_tool_has_an_event_a_product_name_and_a_module()
	{
		foreach (var tool in Enum.GetValues<PowerToysTool>())
		{
			Assert.That(PowerToysEvents.NameOf(tool), Does.StartWith(@"Local\"), tool.ToString());
			Assert.That(PowerToysEvents.ProductName(tool), Is.Not.Empty, tool.ToString());
			Assert.That(PowerToysEvents.ModuleOf(tool), Is.Not.Empty, tool.ToString());
		}
	}
}
