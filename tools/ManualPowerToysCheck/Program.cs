using System.Runtime.Versioning;
using KeystoneDigital.PowerToysController;
using KeystoneDigital.PowerToysController.Awake;
using KeystoneDigital.PowerToysController.Events;
using Microsoft.Extensions.DependencyInjection;

[assembly: SupportedOSPlatform("windows")]

// Live checks against the real PowerToys on this machine. Never run by the test suite. Everything except
// --state changes something visible, so run those only with the user watching; each one restores itself.
var services = new ServiceCollection();
services.AddSingleton<Serilog.ILogger>(Serilog.Core.Logger.None);
services.AddPowerToysServices();
using var provider = services.BuildServiceProvider();
var pt = provider.GetRequiredService<PowerToysComponents>();
await pt.State.RefreshAsync(CancellationToken.None);

switch (args)
{
	case ["--state"]:
	{
		var s = pt.State.Current;
		Console.WriteLine($"status={s.Status} version={s.Version} awake={s.Awake?.Mode} theme={s.Theme}");
		Console.WriteLine("enabled: " + string.Join(", ", s.Enabled.Where(kv => kv.Value).Select(kv => kv.Key)));
		foreach (var m in await pt.FancyZones.MonitorsAsync(CancellationToken.None)) Console.WriteLine($"monitor {m.Index}: {m.Monitor} -> {m.ActiveLayoutName}");
		Console.WriteLine("layouts: " + string.Join(", ", pt.FancyZones.Layouts().Select(l => l.Name)));
		Console.WriteLine("workspaces: " + string.Join(", ", pt.Workspaces.List().Select(w => w.Name)));
		return 0;
	}

	case ["--fire", var tool] when Enum.TryParse<PowerToysTool>(tool, out var parsed):
		Console.WriteLine($"{parsed}: {pt.Trigger.Trigger(parsed)}");
		return 0;

	case ["--awake-roundtrip"]:
	{
		var before = pt.Awake.Read();
		Console.WriteLine($"before: {before}");
		Console.WriteLine($"timed 5 min: {await pt.Awake.SetAsync(AwakeMode.Timed, false, 5, null, CancellationToken.None)}");
		await Task.Delay(3000);
		Console.WriteLine($"restore: {await pt.Awake.SetAsync(before?.Mode ?? AwakeMode.Off, before?.KeepDisplayOn ?? false, before is null ? 1 : before.Hours * 60 + before.Minutes, before?.Until, CancellationToken.None)}");
		return 0;
	}

	case ["--theme-roundtrip"]:
		Console.WriteLine($"toggle: {await pt.Theme.ApplyAsync("toggle", CancellationToken.None)}");
		await Task.Delay(3000);
		Console.WriteLine($"toggle back: {await pt.Theme.ApplyAsync("toggle", CancellationToken.None)}");
		return 0;

	case ["--layout-roundtrip", var index] when int.TryParse(index, out var monitorIndex):
	{
		var monitor = (await pt.FancyZones.MonitorsAsync(CancellationToken.None)).Single(m => m.Index == monitorIndex);
		Console.WriteLine($"monitor {monitor.Index} {monitor.Monitor} currently {monitor.ActiveLayoutName} ({monitor.ActiveLayoutId})");
		Console.WriteLine($"apply columns: {await pt.FancyZones.ApplyAsync("columns", monitor.Key, CancellationToken.None)} {pt.FancyZones.LastCliOutput?.Trim()}");
		await Task.Delay(3000);
		if (monitor.ActiveLayoutId is { } original)
		{
			Console.WriteLine($"restore: {await pt.FancyZones.ApplyAsync(original, monitor.Key, CancellationToken.None)} {pt.FancyZones.LastCliOutput?.Trim()}");
		}

		return 0;
	}

	case ["--module-roundtrip", var module]:
		Console.WriteLine($"toggle {module}: {await pt.Modules.SetAsync(module, "toggle", CancellationToken.None)}");
		Console.WriteLine($"toggle back: {await pt.Modules.SetAsync(module, "toggle", CancellationToken.None)}");
		return 0;

	default:
		Console.WriteLine("Usage: ManualPowerToysCheck --state | --fire <Tool> | --awake-roundtrip | --theme-roundtrip | --layout-roundtrip <monitor> | --module-roundtrip <Module>");
		return 2;
}
