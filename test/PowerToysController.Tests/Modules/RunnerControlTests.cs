using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.Tests.Workspaces;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Modules;

internal sealed class FakeProcessTable : IProcessTable
{
	public List<RunningProcess> Processes { get; } = [];

	public List<int> Killed { get; } = [];

	public HashSet<int> Unkillable { get; } = [];

	public int CurrentSession { get; set; } = 7;

	public IReadOnlyList<RunningProcess> List() => [.. Processes];

	public void Kill(int pid)
	{
		Killed.Add(pid);
		if (!Unkillable.Contains(pid)) Processes.RemoveAll(p => p.Pid == pid);
	}
}

[TestFixture]
public sealed class RunnerControlTests
{
	private static readonly int[] Ours = [10, 11, 12];
	private static readonly int[] Others = [20, 30, 40];

	private static FakeProcessTable Table()
	{
		var table = new FakeProcessTable();
		table.Processes.AddRange(
		[
			new RunningProcess(10, "PowerToys", 7),
			new RunningProcess(11, "PowerToys.ColorPickerUI", 7),
			new RunningProcess(12, "PowerToys.PowerLauncher", 7),
			new RunningProcess(20, "notepad", 7),
			new RunningProcess(30, "PowerToys", 3),
			new RunningProcess(40, "PowerToys.MouseWithoutBordersService", 0),
		]);
		return table;
	}

	[Test]
	public async Task Stop_kills_only_PowerToys_processes_in_this_session()
	{
		var table = Table();
		var control = new WindowsRunnerControl(new FakeStarter(), table, TimeSpan.FromMilliseconds(200));

		var stopped = await control.StopAsync(default);

		Assert.That(stopped, Is.True);
		Assert.That(table.Killed, Is.EquivalentTo(Ours));
		Assert.That(table.Processes.Select(p => p.Pid), Is.EquivalentTo(Others));
	}

	[Test]
	public async Task Stop_reports_false_when_the_runner_survives()
	{
		var table = Table();
		table.Unkillable.Add(10);
		var control = new WindowsRunnerControl(new FakeStarter(), table, TimeSpan.FromMilliseconds(200));

		Assert.That(await control.StopAsync(default), Is.False);
	}

	[Test]
	public void Runner_checks_ignore_other_sessions()
	{
		var table = new FakeProcessTable();
		table.Processes.Add(new RunningProcess(30, "PowerToys", 3));
		var processes = new KeystoneDigital.PowerToysController.Locator.WindowsPowerToysProcesses(table);

		Assert.That(processes.IsRunnerRunning(), Is.False);
		Assert.That(processes.IsRunnerElevated(), Is.False);

		table.Processes.Add(new RunningProcess(10, "PowerToys", 7));
		Assert.That(processes.RunnerProcessIds().Single(), Is.EqualTo(10));
	}

	[Test]
	public void Start_launches_the_runner_through_the_starter()
	{
		var starter = new FakeStarter();
		var control = new WindowsRunnerControl(starter, Table(), TimeSpan.FromMilliseconds(200));

		Assert.That(control.Start(@"C:\PT\PowerToys.exe"), Is.True);
		Assert.That(starter.Started.Single().CommandLine, Is.EqualTo("\"C:\\PT\\PowerToys.exe\""));
	}
}
