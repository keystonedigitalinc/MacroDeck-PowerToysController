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

	public async Task StopAsync(CancellationToken ct)
	{
		await Task.Delay(5, ct);
		Stops++;
		Running = false;
		OnStop?.Invoke();
	}

	public bool Start(string runnerExe)
	{
		Starts++;
		if (!StartFails) Running = true;
		return !StartFails;
	}

	public bool IsRunnerRunning() => Running;

	public bool IsRunnerElevated() => false;

	public IReadOnlyList<int> RunnerProcessIds() => Running ? [1234] : [];
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
	public async Task Runner_not_back_in_time_is_Timeout()
	{
		var (toggle, runner, _) = Create();
		runner.StartFails = true;

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
}
