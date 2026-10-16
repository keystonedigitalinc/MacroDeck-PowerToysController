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

/// <summary>
/// Light Switch exposes only a toggle event, so "dark" and "light" read the current theme first and
/// toggle only when it differs. A toggle counts once the registry shows the flip.
/// </summary>
internal sealed class ThemeControl(EventTrigger trigger, IThemeReader reader, TimeProvider time, TimeSpan? confirmWindow = null) : IDisposable
{
	private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

	private readonly TimeSpan _confirmWindow = confirmWindow ?? TimeSpan.FromSeconds(3);
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
				if (reader.Current() == target) return ThemeOutcome.Applied;
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
