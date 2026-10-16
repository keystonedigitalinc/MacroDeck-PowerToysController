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
	Timeout,
}

/// <summary>
/// PowerToys reads its module map only at startup and accepts live changes only from its own signed
/// Settings app, so a toggle stops PowerToys, writes the map, and starts it again. Stopping comes first
/// so PowerToys cannot write its own copy of the settings over ours on the way out. A PowerToys that
/// was not running stays closed; the change applies on its next start.
/// </summary>
internal sealed class ModuleToggle(
	IFileStore files,
	Func<PowerToysPaths?> paths,
	IPowerToysProcesses processes,
	IRunnerControl runner,
	Func<string, bool> isPolicyForced,
	TimeProvider time,
	TimeSpan? restartWindow = null) : IDisposable
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
			if (files.Read(location.GeneralSettings) is not { } json) return ModuleOutcome.NotInstalled;

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
				files.Write(location.GeneralSettings, EnableMap.WithModule(json, module, target));
				return ModuleOutcome.Applied;
			}

			Volatile.Write(ref _restarting, 1);
			try
			{
				await runner.StopAsync(ct).ConfigureAwait(false);

				// Read again: PowerToys may have saved its settings while it was stopping.
				var latest = files.Read(location.GeneralSettings) ?? json;
				files.Write(location.GeneralSettings, EnableMap.WithModule(latest, module, target));
				runner.Start(location.RunnerExe);

				var deadline = time.GetUtcNow() + _restartWindow;
				while (time.GetUtcNow() < deadline)
				{
					if (processes.IsRunnerRunning() && Holds(location, module, target)) return ModuleOutcome.Applied;
					await Task.Delay(Poll, ct).ConfigureAwait(false);
				}

				return processes.IsRunnerRunning() && Holds(location, module, target) ? ModuleOutcome.Applied : ModuleOutcome.Timeout;
			}
			finally
			{
				Volatile.Write(ref _restarting, 0);
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	public void Dispose() => _gate.Dispose();

	private bool Holds(PowerToysPaths location, string module, bool target) =>
		files.Read(location.GeneralSettings) is { } json && EnableMap.Read(json).TryGetValue(module, out var value) && value == target;
}
