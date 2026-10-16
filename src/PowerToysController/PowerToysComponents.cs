using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.State;
using KeystoneDigital.PowerToysController.Theme;

namespace KeystoneDigital.PowerToysController;

/// <summary>Everything the integration drives, built once per process.</summary>
internal sealed record PowerToysComponents(
	PowerToysLocator Locator,
	StateService State,
	EventTrigger Trigger,
	AwakeControl Awake,
	ThemeControl Theme,
	ModuleToggle Modules);
