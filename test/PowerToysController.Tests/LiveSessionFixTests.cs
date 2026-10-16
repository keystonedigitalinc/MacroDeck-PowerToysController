using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using MacroDeck.Sdk.Actions;
using NUnit.Framework;
using static KeystoneDigital.PowerToysController.Tests.Actions.Phase1ActionTests;

namespace KeystoneDigital.PowerToysController.Tests;

/// <summary>Regression tests for problems found in the live session with real PowerToys.</summary>
[TestFixture]
public sealed class LiveSessionFixTests
{
	// Awake watches its settings file for in-place writes; a temp-file swap went unnoticed.
	[Test]
	public void A_disk_write_raises_a_change_on_the_file_itself()
	{
		var dir = Path.Combine(Path.GetTempPath(), "pt-store-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var target = Path.Combine(dir, "settings.json");
		File.WriteAllText(target, "{}");

		try
		{
			using var changed = new ManualResetEventSlim();
			using var watcher = new FileSystemWatcher(dir, "settings.json") { NotifyFilter = NotifyFilters.LastWrite };
			watcher.Changed += (_, _) => changed.Set();
			watcher.EnableRaisingEvents = true;

			new DiskFileStore().Write(target, "{\"a\":1}");

			Assert.That(changed.Wait(TimeSpan.FromSeconds(2)), Is.True);
			Assert.That(File.ReadAllText(target), Is.EqualTo("{\"a\":1}"));
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}

	// Awake ignores its settings while the module is off, so a write there is not a success.
	[Test]
	public async Task Awake_with_the_module_off_is_Unavailable_and_writes_nothing()
	{
		var (harness, rig) = await ReadyAsync(r => r.Files.Files[TestRig.Paths.GeneralSettings] =
			EnableMap.WithModule(r.Files.Files[TestRig.Paths.GeneralSettings], "Awake", false));
		await using var _h = harness;
		var before = rig.Files.Writes;

		var outcome = await harness.Actions.ExecuteAsync("awake", P(("mode", "timed"), ("minutes", 45)));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.Unavailable));
		Assert.That(rig.Files.Writes, Is.EqualTo(before));
	}
}
