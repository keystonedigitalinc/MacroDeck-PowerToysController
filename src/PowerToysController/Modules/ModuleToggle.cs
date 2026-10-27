using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;

namespace KeystoneDigital.PowerToysController.Modules;

internal enum ModuleOutcome
{
	Applied,
	NoChange,
	NotFound,
	PolicyForced,
	NotInstalled,
	NotStopped,
	StartFailed,
	Failed,
	Timeout,
}

/// <summary>
/// PowerToys reads its module map only at startup and accepts live changes only from its own signed
/// Settings app. A toggle first flips the module's switch in that Settings app, which applies it live.
/// When that is not possible, it stops PowerToys, writes the map, and starts it again. Stopping comes first
/// so PowerToys cannot write its own copy of the settings over ours on the way out. Once PowerToys has
/// been stopped it is always started again, whatever fails in between. A PowerToys that was not running
/// stays closed; the change applies on its next start.
/// </summary>
internal sealed class ModuleToggle(
	IFileStore files,
	Func<PowerToysPaths?> paths,
	IPowerToysProcesses processes,
	IRunnerControl runner,
	Func<string, bool> isPolicyForced,
	TimeProvider time,
	TimeSpan? restartWindow = null,
	ISettingsUi? settingsUi = null) : IDisposable
{
	private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

	private readonly TimeSpan _restartWindow = restartWindow ?? TimeSpan.FromSeconds(15);
	private readonly SemaphoreSlim _gate = new(1, 1);
	private int _restarting;

	public bool IsRestarting => Volatile.Read(ref _restarting) == 1;

	public async Task<ModuleOutcome> SetAsync(string module, string state, CancellationToken ct)
	{
		await _gate.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			if (paths() is not { } location) return ModuleOutcome.NotInstalled;
			if (TryRead(location) is not { } json) return ModuleOutcome.Failed;

			var map = EnableMap.Read(json);
			if (!map.TryGetValue(module, out var current)) return ModuleOutcome.NotFound;
			if (isPolicyForced(module)) return ModuleOutcome.PolicyForced;

			var target = state switch
			{
				"on" => true,
				"off" => false,
				_ => !current,
			};
			if (target == current) return ModuleOutcome.NoChange;

			if (!processes.IsRunnerRunning())
			{
				return TryWrite(location, json, module, target) ? ModuleOutcome.Applied : ModuleOutcome.Failed;
			}

			if (settingsUi is not null)
			{
				switch (await settingsUi.SetAsync(location, module, target, ct).ConfigureAwait(false))
				{
					case SettingsUiOutcome.Applied or SettingsUiOutcome.NoChange:
						return await HeldWithinAsync(location, module, target, _restartWindow).ConfigureAwait(false) ? ModuleOutcome.Applied : ModuleOutcome.Timeout;
					case SettingsUiOutcome.Failed when Holds(location, module, target):
						return ModuleOutcome.Applied;
				}
			}

			return await RestartWithAsync(location, json, module, target, ct).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task<bool> HeldWithinAsync(PowerToysPaths location, string module, bool target, TimeSpan window)
	{
		var deadline = time.GetUtcNow() + window;
		while (!Holds(location, module, target))
		{
			if (time.GetUtcNow() >= deadline) return false;
			await Task.Delay(Poll, CancellationToken.None).ConfigureAwait(false);
		}

		return true;
	}

	public void Dispose() => _gate.Dispose();

	private async Task<ModuleOutcome> RestartWithAsync(PowerToysPaths location, string json, string module, bool target, CancellationToken ct)
	{
		var before = processes.RunnerProcessIds().ToHashSet();
		Volatile.Write(ref _restarting, 1);

		try
		{
			// Past this point PowerToys may be down, so cancellation must not stop us from bringing it back.
			if (!await runner.StopAsync(CancellationToken.None).ConfigureAwait(false)) return ModuleOutcome.NotStopped;

			// PowerToys may have saved its settings while stopping, or been cut off half way through a write;
			// a document that no longer parses is replaced from the copy read before the stop.
			var latest = TryRead(location);
			var written = TryWrite(location, latest is not null && EnableMap.Read(latest).Count > 0 ? latest : json, module, target);

			var started = await Task.Run(() => runner.Start(location.RunnerExe) || runner.Start(location.RunnerExe), CancellationToken.None).ConfigureAwait(false);
			if (!started) return ModuleOutcome.StartFailed;
			if (!written) return ModuleOutcome.Failed;

			var deadline = time.GetUtcNow() + _restartWindow;
			while (true)
			{
				if (IsNewRunner(before) && Holds(location, module, target)) return ModuleOutcome.Applied;
				if (time.GetUtcNow() >= deadline) return ModuleOutcome.Timeout;

				await Task.Delay(Poll, CancellationToken.None).ConfigureAwait(false);
			}
		}
		finally
		{
			Volatile.Write(ref _restarting, 0);
		}
	}

	private bool IsNewRunner(HashSet<int> before) => processes.RunnerProcessIds().Any(pid => !before.Contains(pid));

	private string? TryRead(PowerToysPaths location)
	{
		try
		{
			return files.Read(location.GeneralSettings);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private bool TryWrite(PowerToysPaths location, string json, string module, bool target)
	{
		try
		{
			files.Write(location.GeneralSettings, EnableMap.WithModule(json, module, target));
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
		{
			return false;
		}
	}

	private bool Holds(PowerToysPaths location, string module, bool target) =>
		TryRead(location) is { } json && EnableMap.Read(json).TryGetValue(module, out var value) && value == target;
}
