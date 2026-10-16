using System.Diagnostics;
using KeystoneDigital.PowerToysController.Actions;
using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.FancyZones;
using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Processes;
using KeystoneDigital.PowerToysController.Tests.Awake;
using KeystoneDigital.PowerToysController.Tests.FancyZones;
using KeystoneDigital.PowerToysController.Tests.Locator;
using KeystoneDigital.PowerToysController.Tests.Workspaces;
using KeystoneDigital.PowerToysController.Workspaces;
using MacroDeck.Sdk.Actions;
using NUnit.Framework;
using static KeystoneDigital.PowerToysController.Tests.Actions.Phase1ActionTests;

namespace KeystoneDigital.PowerToysController.Tests;

/// <summary>Regression tests for the findings of the final review.</summary>
[TestFixture]
public sealed class ReviewFixTests
{
	// Finding 5: paths are looked up per call, so an install after Macro Deck started (or none) is handled.
	[Test]
	public async Task FancyZones_without_PowerToys_is_NotInstalled_and_runs_nothing()
	{
		var files = new FakeFileStore();
		FancyZonesTests.Seed(files);
		var cli = new FakeCli(files, TestRig.Paths);
		var control = new FancyZonesControl(files, () => null, cli, new FakeSignals(), TimeProvider.System, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

		Assert.That(await control.ApplyAsync("columns", null, default), Is.EqualTo(LayoutOutcome.NotInstalled));
		Assert.That(cli.Calls, Is.Empty);
	}

	[Test]
	public void Workspaces_without_PowerToys_is_NotInstalled()
	{
		var files = new FakeFileStore();
		var starter = new FakeStarter();
		var control = new WorkspacesControl(files, () => null, starter);

		Assert.That(control.Launch("{AAAAAAAA-1111-2222-3333-444444444444}"), Is.EqualTo(WorkspaceOutcome.NotInstalled));
		Assert.That(starter.Started, Is.Empty);
	}

	// Finding 6: Windows errors become results, never escape.
	[Test]
	public async Task Awake_retries_an_access_denied_write()
	{
		var files = new FakeFileStore();
		files.Files[TestRig.Paths.AwakeSettings] = Fixture.Text("awake-settings.json");
		files.WriteErrorsOnce.Enqueue(new UnauthorizedAccessException("in use"));
		var awake = new AwakeControl(files, TestRig.Paths, TimeProvider.System);

		Assert.That(await awake.SetAsync(AwakeMode.Indefinite, false, 0, null, default), Is.True);
	}

	[Test]
	public async Task A_missing_command_line_tool_is_a_failed_run_not_an_exception()
	{
		var (exit, output) = await new ProcessRunner().RunAsync(@"C:\definitely-not-here\FancyZonesCLI.exe", "get-layouts", TimeSpan.FromSeconds(5), default);

		Assert.That(exit, Is.Not.Zero);
		Assert.That(output, Is.Not.Empty);
	}

	[Test]
	public void A_failed_disk_write_leaves_no_temp_file()
	{
		var dir = Path.Combine(Path.GetTempPath(), "pt-store-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var target = Path.Combine(dir, "settings.json");
		File.WriteAllText(target, "{}");
		File.SetAttributes(target, FileAttributes.ReadOnly);

		try
		{
			Assert.Catch(() => new DiskFileStore().Write(target, "{\"a\":1}"));
			Assert.That(Directory.GetFiles(dir), Is.EquivalentTo(new[] { target }));
		}
		finally
		{
			File.SetAttributes(target, FileAttributes.Normal);
			Directory.Delete(dir, recursive: true);
		}
	}

	[Test]
	public void A_starter_that_throws_is_a_failed_launch()
	{
		var files = new FakeFileStore();
		files.Files[TestRig.Paths.WorkspacesFile] = WorkspacesTests.Sample;
		var control = new WorkspacesControl(files, () => TestRig.Paths, new FakeStarter { Throw = true });

		Assert.That(control.Launch("{AAAAAAAA-1111-2222-3333-444444444444}"), Is.EqualTo(WorkspaceOutcome.Failed));
	}

	// Finding 7: two identical monitors without serials are told apart; an ambiguous button fails.
	[Test]
	public void Identical_monitors_without_serials_get_distinct_keys_and_resolve_correctly()
	{
		var editor = """{"monitors":[{"monitor":"DELL","monitor-instance-id":"I1","monitor-serial-number":"","monitor-number":1,"virtual-desktop":"{00000000-0000-0000-0000-000000000000}"},{"monitor":"DELL","monitor-instance-id":"I2","monitor-serial-number":"","monitor-number":2,"virtual-desktop":"{00000000-0000-0000-0000-000000000000}"}]}""";
		var monitors = FancyZonesData.Monitors(editor, null, []);

		Assert.That(monitors[0].Key, Is.Not.EqualTo(monitors[1].Key));
		Assert.That(FancyZonesData.Resolve(monitors, monitors[1].Key)?.Index, Is.EqualTo(2));
		Assert.That(FancyZonesData.Resolve(monitors, "DELL|"), Is.Null);
	}

	[Test]
	public void A_replugged_monitor_with_a_new_instance_still_resolves_by_name_and_serial()
	{
		var monitors = FancyZonesData.Monitors(Fixture.Text("fz-editor-parameters.json"), null, []);

		Assert.That(FancyZonesData.Resolve(monitors, "AOC2702|SERIAL-A|OLD-INSTANCE")?.Index, Is.EqualTo(1));
		Assert.That(FancyZonesData.Resolve(monitors, "AOC2702|SERIAL-A")?.Index, Is.EqualTo(1));
	}

	// Finding 8: FancyZones turned off fails clearly instead of a false success.
	[Test]
	public async Task Apply_layout_with_FancyZones_off_is_Unavailable()
	{
		var (harness, rig) = await ReadyAsync(r => r.Files.Files[TestRig.Paths.GeneralSettings] =
			EnableMap.WithModule(r.Files.Files[TestRig.Paths.GeneralSettings], "FancyZones", false));
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("apply-layout", P(("layout", "columns"), ("monitor", "all")));

		Assert.That(ErrorCode(outcome), Is.EqualTo(ActionErrorCodes.Unavailable));
		Assert.That(rig.Cli.Calls, Is.Empty);
	}

	// Finding 9: the WMI launch does not block the executor's caller.
	[Test]
	public async Task Launch_workspace_does_not_block_while_Windows_starts_it()
	{
		var rig = new TestRig();
		rig.Files.Files[TestRig.Paths.WorkspacesFile] = WorkspacesTests.Sample;
		await rig.Components.State.RefreshAsync(default);
		using var release = new ManualResetEventSlim();
		rig.Starter.Gate = release;
		var executor = new LaunchWorkspaceAction(rig.Components).CreateExecutor();
		var context = new ActionExecutionContext { Parameters = new Dictionary<string, object> { ["workspace"] = "{AAAAAAAA-1111-2222-3333-444444444444}" } };

		var watch = Stopwatch.StartNew();
		var pending = executor.ExecuteAsync(context);
		var returnedIn = watch.Elapsed;
		release.Set();
		var result = await pending;

		Assert.That(returnedIn, Is.LessThan(TimeSpan.FromMilliseconds(200)));
		Assert.That(result.Status, Is.EqualTo(ActionResult.Success().Status));
	}

	// Finding 10: the 2 s refresh does not broadcast to every window.
	[Test]
	public async Task State_refresh_does_not_broadcast()
	{
		var rig = new TestRig();

		for (var i = 0; i < 3; i++) await rig.Components.State.RefreshAsync(default);

		Assert.That(rig.Signals.Saves, Is.Zero);
		Assert.That(rig.Components.State.Current.Monitors, Has.Count.EqualTo(2));
	}
}
