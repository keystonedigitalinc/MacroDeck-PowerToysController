using KeystoneDigital.PowerToysController.Events;
using Microsoft.Win32;

namespace KeystoneDigital.PowerToysController.Theme;

internal enum Theme
{
	Light,
	Dark,
}

internal enum ThemeOutcome
{
	Applied,
	NoChange,
	NotRunning,
	Denied,
	Timeout,
}

internal interface IThemeReader
{
	Theme? Current();
}

/// <summary>The apps theme, which Light Switch flips together with or instead of the system theme.</summary>
internal sealed class RegistryThemeReader : IThemeReader
{
	public Theme? Current()
	{
		using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
		return key?.GetValue("AppsUseLightTheme") is int light ? (light == 0 ? Theme.Dark : Theme.Light) : null;
	}
}

internal interface ILightSwitchSignal
{
	/// <summary>
	/// Starts waiting for Light Switch to finish a toggle; true when it did within <paramref name="timeout"/>.
	/// Null when Light Switch offers no such signal.
	/// </summary>
	Task<bool>? WaitForToggleDone(TimeSpan timeout);
}

/// <summary>
/// Light Switch sets its manual-reset override event as the last step of every toggle. Its service
/// resets it, but a set releases every thread already waiting, so waiting here takes nothing from it.
/// </summary>
internal sealed class LightSwitchOverrideSignal : ILightSwitchSignal
{
	private const string OverrideEvent = "POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE";

	public Task<bool>? WaitForToggleDone(TimeSpan timeout)
	{
		EventWaitHandle? opened;
		try
		{
			if (!EventWaitHandle.TryOpenExisting(OverrideEvent, out opened)) return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}

		var handle = opened;
		var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		RegisteredWaitHandle? registration = null;
		registration = ThreadPool.RegisterWaitForSingleObject(handle, (_, timedOut) =>
		{
			done.TrySetResult(!timedOut);
			registration?.Unregister(null);
			handle.Dispose();
		}, null, timeout, executeOnlyOnce: true);
		return done.Task;
	}
}

/// <summary>
/// Light Switch exposes only a toggle event, so "dark" and "light" read the current theme first and
/// toggle only when it differs. A toggle counts once the registry shows the flip and Light Switch has
/// finished: its toggle event is auto-reset and is reset again after each toggle, so a toggle sent while
/// one is still running is lost.
/// </summary>
internal sealed class ThemeControl(EventTrigger trigger, IThemeReader reader, ILightSwitchSignal signal, TimeProvider time, TimeSpan? confirmWindow = null) : IDisposable
{
	// On PowerToys 0.101 the apps theme flipped 4.7 to 6 s after the toggle, and the toggle ended about 9 s after it.
	internal static readonly TimeSpan DefaultConfirmWindow = TimeSpan.FromSeconds(15);

	private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

	private readonly TimeSpan _confirmWindow = confirmWindow ?? DefaultConfirmWindow;
	private readonly SemaphoreSlim _gate = new(1, 1);

	public Theme? Current() => reader.Current();

	public async Task<ThemeOutcome> ApplyAsync(string mode, CancellationToken ct)
	{
		await _gate.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			var before = reader.Current();
			var target = mode switch
			{
				"dark" => Theme.Dark,
				"light" => Theme.Light,
				_ => before == Theme.Dark ? Theme.Light : Theme.Dark,
			};

			if (before == target) return ThemeOutcome.NoChange;

			// Waiting starts before the toggle, so the end of this toggle cannot slip past.
			var finished = signal.WaitForToggleDone(_confirmWindow);

			switch (trigger.Trigger(PowerToysTool.LightSwitchToggle))
			{
				case TriggerResult.NotRunning:
					return ThemeOutcome.NotRunning;
				case TriggerResult.Denied:
					return ThemeOutcome.Denied;
			}

			var deadline = time.GetUtcNow() + _confirmWindow;
			while (time.GetUtcNow() < deadline)
			{
				if (reader.Current() == target && (finished is null || finished.IsCompleted)) return ThemeOutcome.Applied;
				await Task.Delay(Poll, ct).ConfigureAwait(false);
			}

			return reader.Current() == target ? ThemeOutcome.Applied : ThemeOutcome.Timeout;
		}
		finally
		{
			_gate.Release();
		}
	}

	public void Dispose() => _gate.Dispose();
}
