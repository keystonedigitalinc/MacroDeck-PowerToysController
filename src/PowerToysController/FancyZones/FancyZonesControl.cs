using System.Globalization;
using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Processes;
using KeystoneDigital.PowerToysController.State;

namespace KeystoneDigital.PowerToysController.FancyZones;

internal enum LayoutOutcome
{
	Applied,
	NotFound,
	NotInstalled,
	NotRunning,
	CliFailed,
	Timeout,
}

/// <summary>
/// Reads monitors and layouts from FancyZones' files and applies a layout through FancyZonesCLI, which
/// merges applied-layouts.json and broadcasts the reload message the way FancyZones' editor does.
/// </summary>
internal sealed class FancyZonesControl(
	IFileStore files,
	Func<PowerToysPaths?> paths,
	IProcessRunner runner,
	IFancyZonesSignals signals,
	TimeProvider time,
	TimeSpan? saveWait = null,
	TimeSpan? confirmWindow = null) : IDisposable
{
	private static readonly TimeSpan CliTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

	private readonly TimeSpan _saveWait = saveWait ?? TimeSpan.FromMilliseconds(300);
	private readonly TimeSpan _confirmWindow = confirmWindow ?? TimeSpan.FromSeconds(2);
	private readonly SemaphoreSlim _gate = new(1, 1);

	public string? LastCliOutput { get; private set; }

	public IReadOnlyList<LayoutInfo> Layouts() =>
		FancyZonesData.Layouts(Read("custom-layouts.json"), Read("layout-templates.json"));

	/// <summary>Asks FancyZones to re-save its monitor list first, so a monitor plugged in since is included.</summary>
	public async Task<IReadOnlyList<MonitorLayout>> MonitorsAsync(CancellationToken ct)
	{
		signals.RequestEditorParametersSave();
		if (_saveWait > TimeSpan.Zero) await Task.Delay(_saveWait, ct).ConfigureAwait(false);

		return CurrentMonitors();
	}

	/// <summary>Applies <paramref name="layoutId"/> to the monitor with <paramref name="monitorKey"/>, or to all monitors when null.</summary>
	public Task<LayoutOutcome> ApplyAsync(string layoutId, string? monitorKey, CancellationToken ct) =>
		ApplyAsync(_ => layoutId, monitorKey, ct);

	/// <summary>
	/// Applies the layout after the target monitor's active one in <paramref name="cycle"/>. With all monitors
	/// the first monitor's active layout decides, and the result goes to every monitor.
	/// </summary>
	public Task<LayoutOutcome> CycleAsync(IReadOnlyList<string> cycle, string? monitorKey, CancellationToken ct) =>
		ApplyAsync(targets => FancyZonesData.Next(cycle, Layouts(), targets[0].ActiveLayoutId), monitorKey, ct);

	// The layout is chosen once the target monitors are known, so a cycle reads their state under the gate.
	private async Task<LayoutOutcome> ApplyAsync(Func<IReadOnlyList<MonitorLayout>, string?> chooseLayout, string? monitorKey, CancellationToken ct)
	{
		await _gate.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			if (paths() is not { } location) return LayoutOutcome.NotInstalled;

			var monitors = await MonitorsAsync(ct).ConfigureAwait(false);
			var target = monitorKey is null ? null : FancyZonesData.Resolve(monitors, monitorKey);
			if (monitorKey is not null && target is null) return LayoutOutcome.NotFound;
			var targets = target is null ? monitors : [target];
			if (targets.Count == 0) return LayoutOutcome.NotFound;

			if (chooseLayout(targets) is not { } layoutId) return LayoutOutcome.NotFound;
			if (!Layouts().Any(l => string.Equals(l.Id, layoutId, StringComparison.OrdinalIgnoreCase))) return LayoutOutcome.NotFound;

			var scope = monitorKey is null ? "--all" : "--monitor " + targets[0].Index.ToString(CultureInfo.InvariantCulture);
			var (exit, output) = await runner.RunAsync(location.FancyZonesCli, $"set-layout {layoutId} {scope}", CliTimeout, ct).ConfigureAwait(false);
			LastCliOutput = output;
			if (exit != 0) return LayoutOutcome.CliFailed;

			var keys = targets.Select(t => t.Key).ToHashSet();
			var deadline = time.GetUtcNow() + _confirmWindow;
			while (true)
			{
				var now = CurrentMonitors().Where(m => keys.Contains(m.Key)).ToList();
				if (now.Count == keys.Count && now.All(m => string.Equals(m.ActiveLayoutId, layoutId, StringComparison.OrdinalIgnoreCase)))
				{
					return LayoutOutcome.Applied;
				}

				if (time.GetUtcNow() >= deadline) return LayoutOutcome.Timeout;
				await Task.Delay(Poll, ct).ConfigureAwait(false);
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	public void Dispose() => _gate.Dispose();

	/// <summary>The monitors as last saved by FancyZones, without asking it to re-save.</summary>
	public IReadOnlyList<MonitorLayout> CurrentMonitors() =>
		FancyZonesData.Monitors(Read("editor-parameters.json"), Read("applied-layouts.json"), Layouts());

	private string? Read(string name)
	{
		try
		{
			return paths() is { } location ? files.Read(Path.Combine(location.FancyZonesDir, name)) : null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}
}
