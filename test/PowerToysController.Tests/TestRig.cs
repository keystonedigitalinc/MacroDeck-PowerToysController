using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.State;
using KeystoneDigital.PowerToysController.Tests.Awake;
using KeystoneDigital.PowerToysController.Tests.Events;
using KeystoneDigital.PowerToysController.Tests.Locator;
using KeystoneDigital.PowerToysController.Tests.Modules;
using KeystoneDigital.PowerToysController.Tests.Theme;
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
		Func<PowerToysPaths?> find = () => installed ? Paths : null;
		var locator = new PowerToysLocator(Runner, find, Files.Read);
		var trigger = new EventTrigger(Events);
		var awake = new AwakeControl(Files, Paths, TimeProvider.System);
		var theme = new ThemeControl(new EventTrigger(Theme), Theme, TimeProvider.System, TimeSpan.FromMilliseconds(300));
		var modules = new ModuleToggle(Files, find, Runner, Runner, _ => false, TimeProvider.System, TimeSpan.FromMilliseconds(300));
		var state = new StateService(locator, awake, theme, modules, Serilog.Core.Logger.None);
		Components = new PowerToysComponents(locator, state, trigger, awake, theme, modules);
	}

	public FakeFileStore Files { get; } = new();

	public FakeRunner Runner { get; } = new();

	public FakeEventOpener Events { get; } = new();

	public SimulatedTheme Theme { get; } = new();

	public PowerToysComponents Components { get; }
}
