using System.Globalization;
using System.Text.Json;
using KeystoneDigital.PowerToysController.State;

namespace KeystoneDigital.PowerToysController.FancyZones;

/// <summary>A custom layout (Id is its uuid) or a built-in template (Id is its type, e.g. "columns").</summary>
internal sealed record LayoutInfo(string Id, string Name, bool IsTemplate);

/// <summary>
/// Reads FancyZones' own JSON files. FancyZonesCLI prints human text with no JSON mode, so state comes
/// from the files the CLI itself reads.
/// </summary>
internal static class FancyZonesData
{
	public const string DefaultVirtualDesktop = "{00000000-0000-0000-0000-000000000000}";

	public static IReadOnlyList<LayoutInfo> Layouts(string? customJson, string? templatesJson)
	{
		var layouts = new List<LayoutInfo>();

		foreach (var layout in Array(customJson, "custom-layouts"))
		{
			if (Str(layout, "uuid") is { } uuid) layouts.Add(new LayoutInfo(uuid, Str(layout, "name") ?? uuid, false));
		}

		foreach (var template in Array(templatesJson, "layout-templates"))
		{
			if (Str(template, "type") is { } type && type != "blank") layouts.Add(new LayoutInfo(type, TemplateName(type), true));
		}

		return layouts;
	}

	/// <summary>
	/// Monitors in FancyZones' own order (the CLI's 1-based index) with their active layout. An entry
	/// with the monitor's exact instance id wins; otherwise the CLI's looser rule applies (same name and
	/// desktop, same serial when both have one, first match), because applied-layouts keeps stale entries.
	/// </summary>
	public static IReadOnlyList<MonitorLayout> Monitors(string? editorParametersJson, string? appliedJson, IReadOnlyList<LayoutInfo> layouts)
	{
		var applied = Array(appliedJson, "applied-layouts");
		var monitors = new List<MonitorLayout>();
		var index = 0;

		foreach (var monitor in Array(editorParametersJson, "monitors"))
		{
			index++;
			var name = Str(monitor, "monitor") ?? "";
			var instance = Str(monitor, "monitor-instance-id");
			var serial = Str(monitor, "monitor-serial-number");
			var desktop = Str(monitor, "virtual-desktop") ?? DefaultVirtualDesktop;

			var match = applied.Cast<JsonElement?>().FirstOrDefault(entry => Device(entry!.Value) is { } d
					&& d.Name == name && d.Instance == instance && d.Desktop == desktop && SerialMatches(d.Serial, serial))
				?? applied.Cast<JsonElement?>().FirstOrDefault(entry => Device(entry!.Value) is { } d
					&& d.Name == name && (d.Desktop == desktop || d.Desktop == DefaultVirtualDesktop) && SerialMatches(d.Serial, serial));

			string? layoutId = null;
			if (match is { } found && found.TryGetProperty("applied-layout", out var layout))
			{
				layoutId = Str(layout, "type") == "custom" ? Str(layout, "uuid") : Str(layout, "type");
			}

			monitors.Add(new MonitorLayout(index, name, serial, desktop, layoutId,
				layoutId is null ? null : layouts.FirstOrDefault(l => string.Equals(l.Id, layoutId, StringComparison.OrdinalIgnoreCase))?.Name ?? layoutId));
		}

		return monitors;
	}

	private static string TemplateName(string type) => type switch
	{
		"focus" => "Focus",
		"columns" => "Columns",
		"rows" => "Rows",
		"grid" => "Grid",
		"priority-grid" => "Priority Grid",
		_ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(type.Replace('-', ' ')),
	};

	private static bool SerialMatches(string? entry, string? monitor) =>
		string.IsNullOrEmpty(entry) || string.IsNullOrEmpty(monitor) || entry == monitor;

	private static (string Name, string? Instance, string? Serial, string Desktop)? Device(JsonElement entry) =>
		entry.TryGetProperty("device", out var device) && device.ValueKind == JsonValueKind.Object
			? (Str(device, "monitor") ?? "", Str(device, "monitor-instance"), Str(device, "serial-number"), Str(device, "virtual-desktop") ?? DefaultVirtualDesktop)
			: null;

	private static List<JsonElement> Array(string? json, string property)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];

		try
		{
			using var document = JsonDocument.Parse(json);
			return document.RootElement.ValueKind == JsonValueKind.Object
				&& document.RootElement.TryGetProperty(property, out var items)
				&& items.ValueKind == JsonValueKind.Array
					? [.. items.EnumerateArray().Select(item => item.Clone())]
					: [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static string? Str(JsonElement element, string property) =>
		element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
}
