using System.Text.Json.Nodes;
using KeystoneDigital.PowerToysController.FancyZones;
using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Processes;
using KeystoneDigital.PowerToysController.Tests.Awake;
using KeystoneDigital.PowerToysController.Tests.Locator;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.FancyZones;

/// <summary>Behaves like FancyZonesCLI set-layout: the new entry goes first and older ones for that monitor are dropped.</summary>
internal sealed class FakeCli(FakeFileStore files, PowerToysPaths paths) : IProcessRunner
{
	public List<string> Calls { get; } = [];

	public int ExitCode { get; set; }

	public bool IgnoreApply { get; set; }

	public Task<(int Exit, string Output)> RunAsync(string exe, string args, TimeSpan timeout, CancellationToken ct)
	{
		Calls.Add(args);
		if (ExitCode != 0) return Task.FromResult((ExitCode, "Error: layout not found"));
		if (IgnoreApply) return Task.FromResult((0, "ok"));

		var parts = args.Split(' ');
		var layout = parts[1];
		var editor = JsonNode.Parse(files.Files[Path.Combine(paths.FancyZonesDir, "editor-parameters.json")])!["monitors"]!.AsArray();
		var targets = parts[2] == "--all" ? editor.Select(m => m!).ToList() : [editor[int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) - 1]!];
		var appliedPath = Path.Combine(paths.FancyZonesDir, "applied-layouts.json");
		var root = JsonNode.Parse(files.Files[appliedPath])!.AsObject();
		var existing = root["applied-layouts"]!.AsArray().Select(n => n!.DeepClone()).ToList();
		var merged = new JsonArray();
		foreach (var monitor in targets)
		{
			var isTemplate = !layout.StartsWith('{');
			merged.Add(new JsonObject
			{
				["device"] = new JsonObject
				{
					["monitor"] = monitor["monitor"]!.GetValue<string>(),
					["monitor-instance"] = monitor["monitor-instance-id"]!.GetValue<string>(),
					["serial-number"] = monitor["monitor-serial-number"]!.GetValue<string>(),
					["monitor-number"] = monitor["monitor-number"]!.GetValue<int>(),
					["virtual-desktop"] = monitor["virtual-desktop"]!.GetValue<string>(),
				},
				["applied-layout"] = new JsonObject
				{
					["uuid"] = isTemplate ? "{00000000-0000-0000-0000-000000000000}" : layout,
					["type"] = isTemplate ? layout : "custom",
				},
			});
		}

		foreach (var entry in existing)
		{
			var replaced = targets.Any(m => entry["device"]!["monitor"]!.GetValue<string>() == m["monitor"]!.GetValue<string>());
			if (!replaced) merged.Add(entry);
		}

		root["applied-layouts"] = merged;
		files.Files[appliedPath] = root.ToJsonString();
		return Task.FromResult((0, "ok"));
	}
}

internal sealed class FakeSignals : IFancyZonesSignals
{
	public int Saves { get; private set; }

	public void RequestEditorParametersSave() => Saves++;
}

[TestFixture]
public sealed class FancyZonesTests
{
	private static readonly string[] CustomNames = ["1 - Primary", "2 - Primary Short", "Stacked", "Stacked Left"];

	private static readonly PowerToysPaths Paths = new(@"C:\PT", @"C:\Data");

	internal static void Seed(FakeFileStore files)
	{
		foreach (var name in new[] { "editor-parameters", "applied-layouts", "custom-layouts", "layout-templates" })
		{
			files.Files[Path.Combine(Paths.FancyZonesDir, name + ".json")] = Fixture.Text($"fz-{name}.json");
		}
	}

	private static (FancyZonesControl Control, FakeCli Cli, FakeSignals Signals, FakeFileStore Files) Create()
	{
		var files = new FakeFileStore();
		Seed(files);
		var cli = new FakeCli(files, Paths);
		var signals = new FakeSignals();
		return (new FancyZonesControl(files, Paths, cli, signals, TimeProvider.System, TimeSpan.Zero, TimeSpan.FromMilliseconds(300)), cli, signals, files);
	}

	[Test]
	public void Reads_four_custom_layouts_with_names_and_the_templates()
	{
		var (control, _, _, _) = Create();

		var layouts = control.Layouts();

		Assert.That(layouts.Where(l => !l.IsTemplate).Select(l => l.Name), Is.EqualTo(CustomNames));
		Assert.That(layouts.Where(l => l.IsTemplate).Select(l => l.Id), Does.Contain("columns").And.Contain("priority-grid"));
	}

	[Test]
	public async Task Monitor_matching_prefers_the_exact_instance_entry()
	{
		var (control, _, signals, _) = await Task.FromResult(Create());

		var monitors = await control.MonitorsAsync(default);

		Assert.That(signals.Saves, Is.EqualTo(1));
		Assert.That(monitors.Select(m => (m.Index, m.Monitor, m.ActiveLayoutName)), Is.EqualTo(new[] { (1, "AOC2702", "Stacked"), (2, "AOC2402", "Stacked") }));
		Assert.That(monitors[0].Key, Is.EqualTo("AOC2702|SERIAL-A"));
	}

	[Test]
	public void Default_virtual_desktop_record_matches_any_desktop()
	{
		var editor = """{"monitors":[{"monitor":"M1","monitor-instance-id":"I1","monitor-serial-number":"S1","monitor-number":1,"virtual-desktop":"{11111111-1111-1111-1111-111111111111}"}]}""";
		var applied = """{"applied-layouts":[{"device":{"monitor":"M1","monitor-instance":"I9","serial-number":"S1","monitor-number":1,"virtual-desktop":"{00000000-0000-0000-0000-000000000000}"},"applied-layout":{"uuid":"{00000000-0000-0000-0000-000000000000}","type":"columns"}}]}""";

		var monitors = FancyZonesData.Monitors(editor, applied, FancyZonesData.Layouts(null, Fixture.Text("fz-layout-templates.json")));

		Assert.That(monitors.Single().ActiveLayoutId, Is.EqualTo("columns"));
	}

	[Test]
	public async Task Apply_runs_set_layout_with_the_monitor_index_and_confirms()
	{
		var (control, cli, _, _) = Create();

		var outcome = await control.ApplyAsync("{E2D55CC1-5F19-4C36-BB72-12C519ACAEA3}", "AOC2702|SERIAL-A", default);

		Assert.That(outcome, Is.EqualTo(LayoutOutcome.Applied));
		Assert.That(cli.Calls.Single(), Is.EqualTo("set-layout {E2D55CC1-5F19-4C36-BB72-12C519ACAEA3} --monitor 1"));
		Assert.That((await control.MonitorsAsync(default))[0].ActiveLayoutName, Is.EqualTo("1 - Primary"));
	}

	[Test]
	public async Task Apply_to_all_monitors_uses_all()
	{
		var (control, cli, _, _) = Create();

		var outcome = await control.ApplyAsync("columns", null, default);

		Assert.That(outcome, Is.EqualTo(LayoutOutcome.Applied));
		Assert.That(cli.Calls.Single(), Is.EqualTo("set-layout columns --all"));
	}

	[Test]
	public async Task Apply_fails_for_a_monitor_that_is_gone()
	{
		var (control, cli, _, _) = Create();

		var outcome = await control.ApplyAsync("{E2D55CC1-5F19-4C36-BB72-12C519ACAEA3}", "DELL9999|SERIAL-Z", default);

		Assert.That(outcome, Is.EqualTo(LayoutOutcome.NotFound));
		Assert.That(cli.Calls, Is.Empty);
	}

	[Test]
	public async Task Apply_fails_for_a_deleted_layout()
	{
		var (control, cli, _, _) = Create();

		Assert.That(await control.ApplyAsync("{DEADBEEF-0000-0000-0000-000000000000}", "AOC2702|SERIAL-A", default), Is.EqualTo(LayoutOutcome.NotFound));
		Assert.That(cli.Calls, Is.Empty);
	}

	[Test]
	public async Task Cli_nonzero_exit_is_CliFailed_with_its_output()
	{
		var (control, cli, _, _) = Create();
		cli.ExitCode = 1;

		var outcome = await control.ApplyAsync("columns", "AOC2702|SERIAL-A", default);

		Assert.That(outcome, Is.EqualTo(LayoutOutcome.CliFailed));
		Assert.That(control.LastCliOutput, Does.Contain("layout not found"));
	}

	[Test]
	public async Task Apply_not_reflected_is_Timeout()
	{
		var (control, cli, _, _) = Create();
		cli.IgnoreApply = true;

		Assert.That(await control.ApplyAsync("columns", "AOC2702|SERIAL-A", default), Is.EqualTo(LayoutOutcome.Timeout));
	}
}
