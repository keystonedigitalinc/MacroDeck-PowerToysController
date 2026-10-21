using KeystoneDigital.PowerToysController.Processes;
using KeystoneDigital.PowerToysController.Tests.Awake;
using KeystoneDigital.PowerToysController.Workspaces;
using MacroDeck.Sdk.Actions;
using NUnit.Framework;
using static KeystoneDigital.PowerToysController.Tests.Actions.Phase1ActionTests;

namespace KeystoneDigital.PowerToysController.Tests.Workspaces;

internal sealed class FakeStarter : IProcessStarter
{
	public List<(string CommandLine, string Directory)> Started { get; } = [];

	public bool Throw { get; set; }

	public ManualResetEventSlim? Gate { get; set; }

	public bool Start(string commandLine, string workingDirectory)
	{
		if (Throw) throw new System.Management.ManagementException("WMI unavailable");
		Gate?.Wait(TimeSpan.FromSeconds(5));
		Started.Add((commandLine, workingDirectory));
		return true;
	}
}

[TestFixture]
public sealed class WorkspacesTests
{
	private static readonly string[] Names = ["Streaming", "Coding"];

	internal const string Sample = """
		{"workspaces":[
		  {"id":"{AAAAAAAA-1111-2222-3333-444444444444}","name":"Streaming","applications":[],"monitor-configuration":[],"creation-time":1,"last-launched-time":0,"is-shortcut-needed":false,"move-existing-windows":false},
		  {"id":"{BBBBBBBB-1111-2222-3333-444444444444}","name":"Coding","applications":[],"monitor-configuration":[],"creation-time":2,"last-launched-time":0,"is-shortcut-needed":false,"move-existing-windows":true}
		]}
		""";

	private static (WorkspacesControl Control, FakeStarter Starter) Create(string? json = Sample)
	{
		var files = new FakeFileStore();
		if (json is not null) files.Files[TestRig.Paths.WorkspacesFile] = json;
		var starter = new FakeStarter();
		return (new WorkspacesControl(files, () => TestRig.Paths, starter), starter);
	}

	[Test]
	public void Lists_workspaces_by_name()
	{
		var (control, _) = Create();

		Assert.That(control.List().Select(w => w.Name), Is.EqualTo(Names));
	}

	[Test]
	public void Missing_file_is_empty_list()
	{
		var (control, _) = Create(json: null);

		Assert.That(control.List(), Is.Empty);
	}

	[Test]
	public void Launch_quotes_the_launcher_and_passes_id_and_1()
	{
		var (control, starter) = Create();

		Assert.That(control.Launch("{BBBBBBBB-1111-2222-3333-444444444444}"), Is.EqualTo(WorkspaceOutcome.Launched));
		Assert.That(starter.Started.Single().CommandLine, Is.EqualTo("\"" + TestRig.Paths.WorkspacesLauncher + "\" {BBBBBBBB-1111-2222-3333-444444444444} 1"));
		Assert.That(starter.Started.Single().Directory, Is.EqualTo(TestRig.Paths.InstallRoot));
	}

	[Test]
	public void Launch_unknown_id_is_NotFound()
	{
		var (control, starter) = Create();

		Assert.That(control.Launch("{CCCCCCCC-0000-0000-0000-000000000000}"), Is.EqualTo(WorkspaceOutcome.NotFound));
		Assert.That(starter.Started, Is.Empty);
	}

	[Test]
	public async Task Launch_workspace_action_starts_the_launcher()
	{
		var (harness, rig) = await ReadyAsync(r => r.Files.Files[TestRig.Paths.WorkspacesFile] = Sample);
		await using var _h = harness;

		var outcome = await harness.Actions.ExecuteAsync("launch-workspace", P(("workspace", "{AAAAAAAA-1111-2222-3333-444444444444}")));

		Assert.That(outcome.Succeeded, Is.True, outcome.Error?.Message);
		Assert.That(rig.Starter.Started.Single().CommandLine, Does.EndWith("{AAAAAAAA-1111-2222-3333-444444444444} 1"));
	}

	[Test]
	public async Task Launch_workspace_deleted_is_NotFound()
	{
		var (harness, _) = await ReadyAsync(r => r.Files.Files[TestRig.Paths.WorkspacesFile] = Sample);
		await using var _h = harness;

		Assert.That(ErrorCode(await harness.Actions.ExecuteAsync("launch-workspace", P(("workspace", "{GONE}")))), Is.EqualTo(ActionErrorCodes.NotFound));
	}
}
