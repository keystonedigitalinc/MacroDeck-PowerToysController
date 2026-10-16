namespace KeystoneDigital.PowerToysController.Locator;

/// <summary>Where PowerToys is installed and where it keeps its settings.</summary>
internal sealed record PowerToysPaths(string InstallRoot, string DataRoot)
{
	public string GeneralSettings => Path.Combine(DataRoot, "settings.json");

	public string AwakeSettings => Path.Combine(DataRoot, "Awake", "settings.json");

	public string FancyZonesDir => Path.Combine(DataRoot, "FancyZones");

	public string WorkspacesFile => Path.Combine(DataRoot, "Workspaces", "workspaces.json");

	public string RunnerExe => Path.Combine(InstallRoot, "PowerToys.exe");

	public string FancyZonesCli => Path.Combine(InstallRoot, "FancyZonesCLI.exe");

	public string WorkspacesLauncher => Path.Combine(InstallRoot, "PowerToys.WorkspacesLauncher.exe");

	public static string DefaultDataRoot =>
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys");
}
