using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.FancyZones;
using KeystoneDigital.PowerToysController.Tests.FancyZones;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.State;
using KeystoneDigital.PowerToysController.Tests.Awake;
using KeystoneDigital.PowerToysController.Tests.Events;
using KeystoneDigital.PowerToysController.Tests.Locator;
using KeystoneDigital.PowerToysController.Tests.Modules;
using KeystoneDigital.PowerToysController.Tests.Theme;
using KeystoneDigital.PowerToysController.Tests.Workspaces;
using KeystoneDigital.PowerToysController.Workspaces;
using KeystoneDigital.PowerToysController.Theme;

namespace KeystoneDigital.PowerToysController.Tests;

/// <summary>Every component wired to fakes: fixture files, a fake runner, fake events and a simulated theme.</summary>
internal sealed class TestRig
{
	public static readonly PowerToysPaths Paths = new(@"C:\PT", @"C:\Data");

	public TestRig(bool installed = true)
	{
		Files.Files[Paths.GeneralSettings] = Fixture.Text("general-settings.json");
		Files.Files[Paths.AwakeSettings] = Fixture.Text("awake-settings.json");
		FancyZonesTests.Seed(Files);
		Cli = new FakeCli(Files, Paths);
		Func<PowerToysPaths?> find = () => installed ? Paths : null;
		var locator = new PowerToysLocator(Runner, find, Files.Read);
		var trigger = new EventTrigger(Events);
		var awake = new AwakeControl(Files, Paths, TimeProvider.System);
		var theme = new ThemeControl(new EventTrigger(Theme), Theme, TimeProvider.System, TimeSpan.FromMilliseconds(300));
		var modules = new ModuleToggle(Files, find, Runner, Runner, _ => false, TimeProvider.System, TimeSpan.FromMilliseconds(300));
		var fancyZones = new FancyZonesControl(Files, find, Cli, Signals, TimeProvider.System, TimeSpan.Zero, TimeSpan.FromMilliseconds(300));
		var state = new StateService(locator, awake, theme, modules, Serilog.Core.Logger.None, _ => Task.FromResult(fancyZones.CurrentMonitors()));
		Components = new PowerToysComponents(locator, state, trigger, awake, theme, modules, fancyZones, new WorkspacesControl(Files, find, Starter));
	}

	public FakeFileStore Files { get; } = new();

	public FakeRunner Runner { get; } = new();

	public FakeCli Cli { get; }

	public FakeStarter Starter { get; } = new();

	public FakeSignals Signals { get; } = new();

	public FakeEventOpener Events { get; } = new();

	public SimulatedTheme Theme { get; } = new();

	public PowerToysComponents Components { get; }
}
