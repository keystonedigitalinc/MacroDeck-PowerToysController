using KeystoneDigital.PowerToysController.Events;
using KeystoneDigital.PowerToysController.Tests.Events;
using KeystoneDigital.PowerToysController.Theme;
using NUnit.Framework;

namespace KeystoneDigital.PowerToysController.Tests.Theme;

/// <summary>
/// A system theme that flips whenever the Light Switch toggle event is set, unless frozen. Like the real
/// Light Switch, a toggle takes <see cref="Busy"/> to finish, signals the end, and a toggle sent before
/// then is lost.
/// </summary>
internal sealed class SimulatedTheme : INamedEventOpener, IThemeReader, ILightSwitchSignal
{
	private readonly List<TaskCompletionSource<bool>> _waiters = [];
	private bool _busy;

	public KeystoneDigital.PowerToysController.Theme.Theme Value { get; set; } = KeystoneDigital.PowerToysController.Theme.Theme.Light;

	public bool Frozen { get; set; }

	public bool HasSignal { get; set; } = true;

	public TimeSpan Busy { get; set; } = TimeSpan.FromMilliseconds(50);

	public int Toggles { get; private set; }

	public int Flips { get; private set; }

	public TriggerResult TrySet(string name)
	{
		Toggles++;
		lock (_waiters)
		{
			if (_busy) return TriggerResult.Triggered;
			_busy = true;
		}

		if (!Frozen)
		{
			Value = Value == KeystoneDigital.PowerToysController.Theme.Theme.Light ? KeystoneDigital.PowerToysController.Theme.Theme.Dark : KeystoneDigital.PowerToysController.Theme.Theme.Light;
			Flips++;
		}

		_ = Task.Delay(Busy).ContinueWith(_ =>
		{
			List<TaskCompletionSource<bool>> done;
			lock (_waiters)
			{
				_busy = false;
				done = [.. _waiters];
				_waiters.Clear();
			}

			foreach (var waiter in done) waiter.TrySetResult(!Frozen);
		}, TaskScheduler.Default);
		return TriggerResult.Triggered;
	}

	public Task<bool>? WaitForToggleDone(TimeSpan timeout)
	{
		if (!HasSignal) return null;

		var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (_waiters) _waiters.Add(waiter);
		_ = Task.Delay(timeout).ContinueWith(_ => waiter.TrySetResult(false), TaskScheduler.Default);
		return waiter.Task;
	}

	public KeystoneDigital.PowerToysController.Theme.Theme? Current() => Value;
}

[TestFixture]
public sealed class ThemeTests
{
	private static ThemeControl Control(SimulatedTheme theme) =>
		new(new EventTrigger(theme), theme, theme, TimeProvider.System, TimeSpan.FromMilliseconds(300));

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
	public async Task Applied_waits_until_Light_Switch_signals_the_toggle_done()
	{
		var theme = new SimulatedTheme { Value = KeystoneDigital.PowerToysController.Theme.Theme.Dark, Busy = TimeSpan.FromMilliseconds(200) };
		var control = new ThemeControl(new EventTrigger(theme), theme, theme, TimeProvider.System, TimeSpan.FromSeconds(2));
		var clock = System.Diagnostics.Stopwatch.StartNew();

		var outcome = await control.ApplyAsync("light", default);

		Assert.That(outcome, Is.EqualTo(ThemeOutcome.Applied));
		Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(150)));
	}

	[Test]
	public async Task Two_toggles_one_after_the_other_both_land()
	{
		var theme = new SimulatedTheme { Value = KeystoneDigital.PowerToysController.Theme.Theme.Dark, Busy = TimeSpan.FromMilliseconds(100) };
		var control = new ThemeControl(new EventTrigger(theme), theme, theme, TimeProvider.System, TimeSpan.FromSeconds(2));

		Assert.That(await control.ApplyAsync("toggle", default), Is.EqualTo(ThemeOutcome.Applied));
		Assert.That(await control.ApplyAsync("toggle", default), Is.EqualTo(ThemeOutcome.Applied));

		Assert.That(theme.Flips, Is.EqualTo(2));
		Assert.That(theme.Value, Is.EqualTo(KeystoneDigital.PowerToysController.Theme.Theme.Dark));
	}

	[Test]
	public async Task Without_the_done_signal_the_registry_alone_confirms()
	{
		var theme = new SimulatedTheme { Value = KeystoneDigital.PowerToysController.Theme.Theme.Dark, HasSignal = false };

		Assert.That(await Control(theme).ApplyAsync("light", default), Is.EqualTo(ThemeOutcome.Applied));
	}

	// Measured on PowerToys 0.101 on 2026-10-17: the registry flips 4.7 to 6 s after the toggle event.
	[Test]
	public void Default_window_outlasts_a_real_Light_Switch_flip()
	{
		Assert.That(ThemeControl.DefaultConfirmWindow, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(12)));
	}

	[Test]
	public async Task Light_Switch_not_running_is_NotRunning()
	{
		var opener = new FakeEventOpener();
		opener.Results[PowerToysEvents.NameOf(PowerToysTool.LightSwitchToggle)] = TriggerResult.NotRunning;
		var reader = new SimulatedTheme();
		var control = new ThemeControl(new EventTrigger(opener), reader, reader, TimeProvider.System, TimeSpan.FromMilliseconds(300));

		Assert.That(await control.ApplyAsync("toggle", default), Is.EqualTo(ThemeOutcome.NotRunning));
	}
}
