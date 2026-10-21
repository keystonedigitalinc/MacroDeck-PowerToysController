using KeystoneDigital.PowerToysController.Processes;

namespace KeystoneDigital.PowerToysController.Modules;

internal sealed record RunningProcess(int Pid, string Name, int Session);

internal interface IProcessTable
{
	int CurrentSession { get; }

	IReadOnlyList<RunningProcess> List();

	/// <summary>Ends one process. Never its process tree: apps a user opened from PowerToys Run are its children.</summary>
	void Kill(int pid);
}

internal sealed class WindowsProcessTable : IProcessTable
{
	public int CurrentSession { get; } = System.Diagnostics.Process.GetCurrentProcess().SessionId;

	public IReadOnlyList<RunningProcess> List()
	{
		var processes = System.Diagnostics.Process.GetProcesses();
		try
		{
			return [.. processes.Select(p => new RunningProcess(p.Id, p.ProcessName, SafeSession(p)))];
		}
		finally
		{
			foreach (var process in processes) process.Dispose();
		}
	}

	public void Kill(int pid)
	{
		try
		{
			using var process = System.Diagnostics.Process.GetProcessById(pid);
			process.Kill();
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
		{
			// Already gone, or not ours to end (an elevated or service process).
		}
	}

	private static int SafeSession(System.Diagnostics.Process process)
	{
		try
		{
			return process.SessionId;
		}
		catch (InvalidOperationException)
		{
			return -1;
		}
	}
}

internal interface IRunnerControl
{
	/// <summary>True once no PowerToys process remains in this session.</summary>
	Task<bool> StopAsync(CancellationToken ct);

	bool Start(string runnerExe);
}

/// <summary>
/// Stops PowerToys and the module processes it hosts in this user's session only, and starts it again
/// outside Macro Deck's plugin job.
/// </summary>
internal sealed class WindowsRunnerControl(IProcessStarter starter, IProcessTable table, TimeSpan? stopWait = null) : IRunnerControl
{
	private readonly TimeSpan _stopWait = stopWait ?? TimeSpan.FromSeconds(5);

	public async Task<bool> StopAsync(CancellationToken ct)
	{
		foreach (var process in Ours()) table.Kill(process.Pid);

		var deadline = DateTime.UtcNow + _stopWait;
		while (true)
		{
			if (Ours().Count == 0) return true;
			if (DateTime.UtcNow >= deadline) return false;

			await Task.Delay(100, ct).ConfigureAwait(false);
		}
	}

	public bool Start(string runnerExe)
	{
		try
		{
			return starter.Start($"\"{runnerExe}\"", Path.GetDirectoryName(runnerExe) ?? "");
		}
		catch (Exception ex) when (ex is System.Management.ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	private List<RunningProcess> Ours() =>
		[.. table.List().Where(p => p.Session == table.CurrentSession
			&& (p.Name == "PowerToys" || p.Name.StartsWith("PowerToys.", StringComparison.Ordinal)))];
}
