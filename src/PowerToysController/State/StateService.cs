using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.Locator;
using KeystoneDigital.PowerToysController.Modules;
using KeystoneDigital.PowerToysController.Theme;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KeystoneDigital.PowerToysController.State;

/// <summary>One monitor as FancyZones numbers it, and the layout applied to it there.</summary>
internal sealed record MonitorLayout(int Index, string Monitor, string? Serial, string VirtualDesktop, string? ActiveLayoutName);

internal sealed record PowerToysSnapshot(
	string Status,
	string? Version,
	AwakeState? Awake,
	Theme.Theme? Theme,
	IReadOnlyDictionary<string, bool> Enabled,
	IReadOnlyList<MonitorLayout> Monitors)
{
	public static PowerToysSnapshot Empty { get; } = new("starting", null, null, null, new Dictionary<string, bool>(), []);
}

/// <summary>
/// Caches what variables and pickers show, so a variable read never touches the disk, the registry or
/// another process. Rebuilt every two seconds.
/// </summary>
internal sealed class StateService(
	PowerToysLocator locator,
	AwakeControl awake,
	ThemeControl theme,
	ModuleToggle modules,
	ILogger logger,
	Func<CancellationToken, Task<IReadOnlyList<MonitorLayout>>>? readMonitors = null) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

	private readonly ILogger _logger = logger.ForContext<StateService>();
	private PowerToysSnapshot _current = PowerToysSnapshot.Empty;

	public PowerToysSnapshot Current => Volatile.Read(ref _current);

	public async Task RefreshAsync(CancellationToken ct)
	{
		var status = locator.Paths is null ? "not_installed"
			: modules.IsRestarting ? "restarting"
			: !locator.Processes.IsRunnerRunning() ? "not_running"
			: locator.Processes.IsRunnerElevated() ? "elevated"
			: "ready";

		var monitors = Current.Monitors;
		if (status == "ready" && readMonitors is not null)
		{
			try
			{
				monitors = await readMonitors(ct).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
			{
				_logger.Debug(ex, "Could not read FancyZones monitors");
			}
		}

		var next = new PowerToysSnapshot(status, locator.Version, awake.Read(), theme.Current(), locator.Modules, monitors);
		var previous = Interlocked.Exchange(ref _current, next);
		if (previous.Status != next.Status)
		{
			_logger.Information("PowerToys status {Previous} -> {Status} (version {Version})", previous.Status, next.Status, next.Version);
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await RefreshAsync(stoppingToken).ConfigureAwait(false);
				await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Could not refresh the PowerToys state");
				await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
			}
		}
	}
}
