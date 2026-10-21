using System.Text.Json;
using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.Variables;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.State;

[TestFixture]
public sealed class StateTests
{
	internal static string? ValueOf(CapabilityInvocationOutcome outcome)
	{
		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		var data = outcome.Data!.Value;
		var value = data.TryGetProperty("value", out var inner) ? inner : data;
		if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var nested)) value = nested;

		// Values travel as a tagged union, e.g. {"kind":"text","text":"ready"}.
		if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("kind", out _))
		{
			value = value.EnumerateObject().Where(p => p.Name != "kind").Select(p => p.Value).FirstOrDefault();
		}

		return value.ValueKind switch
		{
			JsonValueKind.Null or JsonValueKind.Undefined => null,
			JsonValueKind.String => value.GetString(),
			_ => value.GetRawText(),
		};
	}

	[Test]
	public async Task Status_is_ready_with_version_and_modules()
	{
		var rig = new TestRig();

		await rig.Components.State.RefreshAsync(default);

		var snapshot = rig.Components.State.Current;
		Assert.That(snapshot.Status, Is.EqualTo("ready"));
		Assert.That(snapshot.Version, Is.EqualTo("0.101.2362"));
		Assert.That(snapshot.Enabled["ColorPicker"], Is.True);
		Assert.That(snapshot.Awake!.Mode, Is.EqualTo(AwakeMode.Off));
	}

	[Test]
	public async Task Status_is_not_installed_without_paths()
	{
		var rig = new TestRig(installed: false);

		await rig.Components.State.RefreshAsync(default);

		Assert.That(rig.Components.State.Current.Status, Is.EqualTo("not_installed"));
	}

	[Test]
	public async Task Status_is_not_running_when_runner_absent()
	{
		var rig = new TestRig();
		rig.Runner.Running = false;

		await rig.Components.State.RefreshAsync(default);

		Assert.That(rig.Components.State.Current.Status, Is.EqualTo("not_running"));
	}

	[Test]
	public async Task Status_is_elevated_when_the_runner_is_elevated()
	{
		var rig = new TestRig();
		rig.Runner.Elevated = true;

		await rig.Components.State.RefreshAsync(default);

		Assert.That(rig.Components.State.Current.Status, Is.EqualTo("elevated"));
	}

	[Test]
	public void Module_slug_for_Measure_Tool_is_measure_tool()
	{
		Assert.That(PowerToysVariables.Slug("Measure Tool"), Is.EqualTo("measure_tool"));
		Assert.That(PowerToysVariables.LocalId("Measure Tool"), Is.EqualTo("module-measure-tool"));
	}

	[Test]
	public async Task Variables_read_live_values()
	{
		var rig = new TestRig();
		await rig.Components.State.RefreshAsync(default);
		await using var harness = IntegrationTests.CreateHarness(rig);
		await harness.InitializeIntegrationsAsync();

		Assert.That(ValueOf(await harness.Variables.GetAsync("status")), Is.EqualTo("ready"));
		Assert.That(ValueOf(await harness.Variables.GetAsync("version")), Is.EqualTo("0.101.2362"));
		Assert.That(ValueOf(await harness.Variables.GetAsync("awake-mode")), Is.EqualTo("off"));
		Assert.That(ValueOf(await harness.Variables.GetAsync("theme")), Is.EqualTo("light"));
		Assert.That(ValueOf(await harness.Variables.GetAsync("module-colorpicker")), Is.EqualTo("true"));
		Assert.That(ValueOf(await harness.Variables.GetAsync("module-awake")), Is.EqualTo("false"));
	}
}
