using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.Tests.Events;
using KeystoneDigital.PowerToysController.Theme;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Theme;

/// <summary>A system theme that flips whenever the Light Switch toggle event is set, unless frozen.</summary>
internal sealed class SimulatedTheme : INamedEventOpener, IThemeReader
{
	public KeystoneDigital.PowerToysController.Theme.Theme Value { get; set; } = KeystoneDigital.PowerToysController.Theme.Theme.Light;

	public bool Frozen { get; set; }

	public int Toggles { get; private set; }

	public TriggerResult TrySet(string name)
	{
		Toggles++;
		if (!Frozen) Value = Value == KeystoneDigital.PowerToysController.Theme.Theme.Light ? KeystoneDigital.PowerToysController.Theme.Theme.Dark : KeystoneDigital.PowerToysController.Theme.Theme.Light;
		return TriggerResult.Triggered;
	}

	public KeystoneDigital.PowerToysController.Theme.Theme? Current() => Value;
}

[TestFixture]
public sealed class ThemeTests
{
	private static ThemeControl Control(SimulatedTheme theme) =>
		new(new EventTrigger(theme), theme, TimeProvider.System, TimeSpan.FromMilliseconds(300));

	[Test]
	public async Task Dark_when_already_dark_is_NoChange_and_fires_nothing()
	{
		var theme = new SimulatedTheme { Value = KeystoneDigital.PowerToysController.Theme.Theme.Dark };

		var outcome = await Control(theme).ApplyAsync("dark", default);

		Assert.That(outcome, Is.EqualTo(ThemeOutcome.NoChange));
		Assert.That(theme.Toggles, Is.Zero);
	}

	[Test]
	public async Task Light_when_dark_fires_one_toggle_and_confirms()
	{
		var theme = new SimulatedTheme { Value = KeystoneDigital.PowerToysController.Theme.Theme.Dark };

		var outcome = await Control(theme).ApplyAsync("light", default);

		Assert.That(outcome, Is.EqualTo(ThemeOutcome.Applied));
		Assert.That(theme.Toggles, Is.EqualTo(1));
		Assert.That(theme.Value, Is.EqualTo(KeystoneDigital.PowerToysController.Theme.Theme.Light));
	}

	[Test]
	public async Task Two_quick_theme_toggles_end_where_they_started()
	{
		var theme = new SimulatedTheme();
		var control = Control(theme);

		var outcomes = await Task.WhenAll(control.ApplyAsync("toggle", default), control.ApplyAsync("toggle", default));

		Assert.That(outcomes, Is.All.EqualTo(ThemeOutcome.Applied));
		Assert.That(theme.Toggles, Is.EqualTo(2));
		Assert.That(theme.Value, Is.EqualTo(KeystoneDigital.PowerToysController.Theme.Theme.Light));
	}

	[Test]
	public async Task No_flip_within_the_window_is_Timeout()
	{
		var theme = new SimulatedTheme { Frozen = true };

		Assert.That(await Control(theme).ApplyAsync("toggle", default), Is.EqualTo(ThemeOutcome.Timeout));
	}

	[Test]
	public async Task Light_Switch_not_running_is_NotRunning()
	{
		var opener = new FakeEventOpener();
		opener.Results[PowerToysEvents.NameOf(PowerToysTool.LightSwitchToggle)] = TriggerResult.NotRunning;
		var reader = new SimulatedTheme();
		var control = new ThemeControl(new EventTrigger(opener), reader, TimeProvider.System, TimeSpan.FromMilliseconds(300));

		Assert.That(await control.ApplyAsync("toggle", default), Is.EqualTo(ThemeOutcome.NotRunning));
	}
}
