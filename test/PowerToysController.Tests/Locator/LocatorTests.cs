using System.Text.Json.Nodes;
using KeystoneDigital.PowerToysController.Locator;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Locator;

internal static class Fixture
{
	public static string Text(string name) =>
		File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name));
}

[TestFixture]
public sealed class LocatorTests
{
	[Test]
	public void Reads_every_module_from_the_fixture()
	{
		var map = EnableMap.Read(Fixture.Text("general-settings.json"));

		Assert.That(map["ColorPicker"], Is.True);
		Assert.That(map["FancyZones"], Is.True);
		Assert.That(map, Does.ContainKey("Awake"));
		Assert.That(map, Has.Count.EqualTo(35));
	}

	[Test]
	public void WithModule_flips_one_key_and_keeps_the_rest()
	{
		var original = Fixture.Text("general-settings.json");
		var before = EnableMap.Read(original);

		var updated = EnableMap.WithModule(original, "ColorPicker", false);

		var after = EnableMap.Read(updated);
		Assert.That(after["ColorPicker"], Is.False);
		Assert.That(after.Where(kv => kv.Key != "ColorPicker"), Is.EquivalentTo(before.Where(kv => kv.Key != "ColorPicker")));
		var originalNode = JsonNode.Parse(original)!.AsObject();
		var updatedNode = JsonNode.Parse(updated)!.AsObject();
		Assert.That(updatedNode["powertoys_version"]!.GetValue<string>(), Is.EqualTo(originalNode["powertoys_version"]!.GetValue<string>()));
		Assert.That(updatedNode["theme"]!.GetValue<string>(), Is.EqualTo("system"));
	}

	[Test]
	public void Malformed_json_reads_as_empty()
	{
		Assert.That(EnableMap.Read("{oops"), Is.Empty);
		Assert.That(EnableMap.Read("""{"enabled":"nope"}"""), Is.Empty);
	}

	[Test]
	public void Version_reads_without_the_leading_v()
	{
		Assert.That(EnableMap.Version(Fixture.Text("general-settings.json")), Is.EqualTo("0.101.2362"));
	}

	[Test]
	public void Paths_point_into_the_data_and_install_roots()
	{
		var paths = new PowerToysPaths(@"C:\PT", @"C:\Data");

		Assert.That(paths.GeneralSettings, Is.EqualTo(@"C:\Data\settings.json"));
		Assert.That(paths.AwakeSettings, Is.EqualTo(@"C:\Data\Awake\settings.json"));
		Assert.That(paths.WorkspacesFile, Is.EqualTo(@"C:\Data\Workspaces\workspaces.json"));
		Assert.That(paths.RunnerExe, Is.EqualTo(@"C:\PT\PowerToys.exe"));
		Assert.That(paths.FancyZonesCli, Is.EqualTo(@"C:\PT\FancyZonesCLI.exe"));
		Assert.That(paths.WorkspacesLauncher, Is.EqualTo(@"C:\PT\PowerToys.WorkspacesLauncher.exe"));
	}
}
