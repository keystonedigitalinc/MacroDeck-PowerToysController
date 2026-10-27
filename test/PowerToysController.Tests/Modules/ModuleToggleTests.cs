using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.Tests.Awake;
using KeystoneDigital.PowerToysController.Tests.Locator;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Modules;

internal sealed class FakeRunner : IRunnerControl, IPowerToysProcesses
{
	public bool Running { get; set; } = true;

	public bool StartFails { get; set; }

	public int Stops { get; private set; }

	public int Starts { get; private set; }

	public Action? OnStop { get; set; }

	public bool StopSucceeds { get; set; } = true;

	public bool KeepOldPid { get; set; }

	private int _pid = 1234;

	public async Task<bool> StopAsync(CancellationToken ct)
	{
		await Task.Delay(5, ct);
		Stops++;
		if (StopSucceeds) Running = false;
		OnStop?.Invoke();
		return StopSucceeds;
	}

	public bool Start(string runnerExe)
	{
		Starts++;
		if (!StartFails)
		{
			Running = true;
			if (!KeepOldPid) _pid++;
		}
		return !StartFails;
	}

	public bool IsRunnerRunning() => Running;

	public bool Elevated { get; set; }

	public bool IsRunnerElevated() => Running && Elevated;

	public IReadOnlyList<int> RunnerProcessIds() => Running ? [_pid] : [];
}

[TestFixture]
public sealed class ModuleToggleTests
{
	private static readonly PowerToysPaths Paths = new(@"C:\PT", @"C:\Data");

	private static (ModuleToggle Toggle, FakeRunner Runner, FakeFileStore Store) Create(Func<string, bool>? policy = null)
	{
		var store = new FakeFileStore();
		store.Files[Paths.GeneralSettings] = Fixture.Text("general-settings.json");
		var runner = new FakeRunner();
		var toggle = new ModuleToggle(store, () => Paths, runner, runner, policy ?? (_ => false), TimeProvider.System, TimeSpan.FromMilliseconds(300));
		return (toggle, runner, store);
	}

	private static bool Enabled(FakeFileStore store, string module) => EnableMap.Read(store.Files[Paths.GeneralSettings])[module];

	[Test]
	public async Task Off_writes_the_map_and_restarts_a_running_PowerToys()
	{
		var (toggle, runner, store) = Create();

		var outcome = await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.Applied));
		Assert.That(Enabled(store, "ColorPicker"), Is.False);
		Assert.That(runner.Stops, Is.EqualTo(1));
		Assert.That(runner.Starts, Is.EqualTo(1));
		Assert.That(runner.Running, Is.True);
	}

	[Test]
	public async Task The_map_is_written_after_PowerToys_has_stopped()
	{
		var (toggle, runner, store) = Create();
		var stoppedBeforeWrite = false;
		runner.OnStop = () => stoppedBeforeWrite = store.Writes == 0;

		await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(stoppedBeforeWrite, Is.True);
	}

	[Test]
	public async Task Toggle_while_PowerToys_is_closed_writes_only()
	{
		var (toggle, runner, store) = Create();
		runner.Running = false;

		var outcome = await toggle.SetAsync("Awake", "toggle", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.Applied));
		Assert.That(Enabled(store, "Awake"), Is.True);
		Assert.That(runner.Starts, Is.Zero);
		Assert.That(runner.Running, Is.False);
	}

	[Test]
	public async Task Already_in_state_is_NoChange_without_restart()
	{
		var (toggle, runner, store) = Create();

		var outcome = await toggle.SetAsync("ColorPicker", "on", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.NoChange));
		Assert.That(runner.Stops, Is.Zero);
		Assert.That(store.Writes, Is.Zero);
	}

	[Test]
	public async Task Policy_forced_module_is_rejected_without_writing()
	{
		var (toggle, runner, store) = Create(policy: module => module == "ColorPicker");

		var outcome = await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.PolicyForced));
		Assert.That(store.Writes, Is.Zero);
		Assert.That(runner.Stops, Is.Zero);
	}

	[Test]
	public async Task A_runner_that_will_not_stop_is_NotStopped_and_nothing_is_written()
	{
		var (toggle, runner, store) = Create();
		runner.StopSucceeds = false;

		Assert.That(await toggle.SetAsync("ColorPicker", "off", default), Is.EqualTo(ModuleOutcome.NotStopped));
		Assert.That(store.Writes, Is.Zero);
		Assert.That(Enabled(store, "ColorPicker"), Is.True);
	}

	[Test]
	public async Task A_failed_write_still_restarts_PowerToys()
	{
		var (toggle, runner, store) = Create();
		store.WriteError = new IOException("locked");

		var outcome = await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.Failed));
		Assert.That(runner.Starts, Is.EqualTo(1));
		Assert.That(runner.Running, Is.True);
	}

	[Test]
	public async Task A_half_written_settings_file_after_stop_is_repaired_from_the_earlier_read()
	{
		var (toggle, runner, store) = Create();
		runner.OnStop = () => store.Files[Paths.GeneralSettings] = """{"enabled":{"Color""";

		var outcome = await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.Applied));
		Assert.That(Enabled(store, "ColorPicker"), Is.False);
		Assert.That(EnableMap.Read(store.Files[Paths.GeneralSettings]), Has.Count.EqualTo(35));
	}

	[Test]
	public async Task A_start_that_fails_twice_is_StartFailed()
	{
		var (toggle, runner, _) = Create();
		runner.StartFails = true;

		Assert.That(await toggle.SetAsync("ColorPicker", "off", default), Is.EqualTo(ModuleOutcome.StartFailed));
		Assert.That(runner.Starts, Is.EqualTo(2));
	}

	[Test]
	public async Task Cancelling_after_stop_still_restarts_PowerToys()
	{
		var (toggle, runner, _) = Create();
		using var cts = new CancellationTokenSource();
		runner.OnStop = cts.Cancel;

		try
		{
			await toggle.SetAsync("ColorPicker", "off", cts.Token);
		}
		catch (OperationCanceledException)
		{
		}

		Assert.That(runner.Starts, Is.EqualTo(1));
		Assert.That(runner.Running, Is.True);
	}

	[Test]
	public async Task Only_a_new_runner_counts_as_back()
	{
		var (toggle, runner, _) = Create();
		runner.KeepOldPid = true;

		Assert.That(await toggle.SetAsync("ColorPicker", "off", default), Is.EqualTo(ModuleOutcome.Timeout));
	}

	[Test]
	public async Task Unknown_module_is_NotFound()
	{
		var (toggle, _, _) = Create();

		Assert.That(await toggle.SetAsync("NoSuchModule", "on", default), Is.EqualTo(ModuleOutcome.NotFound));
	}

	[Test]
	public async Task Two_quick_module_toggles_end_where_they_started()
	{
		var (toggle, runner, store) = Create();

		var outcomes = await Task.WhenAll(toggle.SetAsync("ColorPicker", "toggle", default), toggle.SetAsync("ColorPicker", "toggle", default));

		Assert.That(outcomes, Is.All.EqualTo(ModuleOutcome.Applied));
		Assert.That(Enabled(store, "ColorPicker"), Is.True);
		Assert.That(runner.Stops, Is.EqualTo(2));
	}

	[Test]
	public async Task Restarting_is_reported_while_the_runner_is_down()
	{
		var (toggle, runner, _) = Create();
		var seen = false;
		runner.OnStop = () => seen = toggle.IsRestarting;

		await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(seen, Is.True);
		Assert.That(toggle.IsRestarting, Is.False);
	}

	private static (ModuleToggle Toggle, FakeRunner Runner, FakeFileStore Store, FakeSettingsUi Ui) CreateWithUi(SettingsUiOutcome outcome)
	{
		var store = new FakeFileStore();
		store.Files[Paths.GeneralSettings] = Fixture.Text("general-settings.json");
		var runner = new FakeRunner();
		var ui = new FakeSettingsUi(store, Paths, outcome);
		var toggle = new ModuleToggle(store, () => Paths, runner, runner, _ => false, TimeProvider.System, TimeSpan.FromMilliseconds(300), ui);
		return (toggle, runner, store, ui);
	}

	[Test]
	public async Task A_switch_flipped_in_Settings_applies_without_a_restart()
	{
		var (toggle, runner, store, ui) = CreateWithUi(SettingsUiOutcome.Applied);

		var outcome = await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.Applied));
		Assert.That(ui.Calls, Is.EqualTo(1));
		Assert.That(Enabled(store, "ColorPicker"), Is.False);
		Assert.That(runner.Stops, Is.Zero);
	}

	[Test]
	public async Task A_module_without_a_Settings_switch_falls_back_to_a_restart()
	{
		var (toggle, runner, store, _) = CreateWithUi(SettingsUiOutcome.Unsupported);

		var outcome = await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.Applied));
		Assert.That(Enabled(store, "ColorPicker"), Is.False);
		Assert.That(runner.Stops, Is.EqualTo(1));
	}

	[Test]
	public async Task A_failed_Settings_attempt_falls_back_to_a_restart()
	{
		var (toggle, runner, store, _) = CreateWithUi(SettingsUiOutcome.Failed);

		var outcome = await toggle.SetAsync("ColorPicker", "off", default);

		Assert.That(outcome, Is.EqualTo(ModuleOutcome.Applied));
		Assert.That(Enabled(store, "ColorPicker"), Is.False);
		Assert.That(runner.Stops, Is.EqualTo(1));
	}

	[Test]
	public async Task A_Settings_switch_that_never_reaches_the_file_is_Timeout_without_a_restart()
	{
		var (toggle, runner, _, ui) = CreateWithUi(SettingsUiOutcome.Applied);
		ui.WritesFile = false;

		Assert.That(await toggle.SetAsync("ColorPicker", "off", default), Is.EqualTo(ModuleOutcome.Timeout));
		Assert.That(runner.Stops, Is.Zero);
	}

	[Test]
	public async Task A_closed_PowerToys_is_written_directly_without_opening_Settings()
	{
		var (toggle, runner, store, ui) = CreateWithUi(SettingsUiOutcome.Applied);
		runner.Running = false;

		Assert.That(await toggle.SetAsync("ColorPicker", "off", default), Is.EqualTo(ModuleOutcome.Applied));
		Assert.That(ui.Calls, Is.Zero);
		Assert.That(Enabled(store, "ColorPicker"), Is.False);
	}
}

/// <summary>Stands in for PowerToys' Settings app: on success the runner saves the map, as PowerToys does.</summary>
internal sealed class FakeSettingsUi(FakeFileStore store, PowerToysPaths paths, SettingsUiOutcome outcome) : ISettingsUi
{
	public int Calls { get; private set; }

	public bool WritesFile { get; set; } = true;

	public Task<SettingsUiOutcome> SetAsync(PowerToysPaths location, string module, bool target, CancellationToken ct)
	{
		Calls++;
		if (outcome == SettingsUiOutcome.Applied && WritesFile)
		{
			store.Files[paths.GeneralSettings] = EnableMap.WithModule(store.Files[paths.GeneralSettings], module, target);
		}

		return Task.FromResult(outcome);
	}
}
