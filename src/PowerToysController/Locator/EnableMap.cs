using System.Text.Json;
using System.Text.Json.Nodes;

namespace KeystoneDigital.PowerToysController.Locator;

/// <summary>
/// The <c>enabled</c> map in PowerToys' general settings.json. The runner reads it only at startup, so a
/// change takes effect on the next PowerToys start.
/// </summary>
internal static class EnableMap
{
	public static IReadOnlyDictionary<string, bool> Read(string generalSettingsJson)
	{
		var map = new Dictionary<string, bool>(StringComparer.Ordinal);
		if (Parse(generalSettingsJson)?["enabled"] is not JsonObject enabled) return map;

		foreach (var (module, value) in enabled)
		{
			if (value is JsonValue flag && flag.TryGetValue<bool>(out var on)) map[module] = on;
		}

		return map;
	}

	/// <summary>The same document with one module's flag set and every other field untouched.</summary>
	public static string WithModule(string generalSettingsJson, string module, bool enabled)
	{
		var root = Parse(generalSettingsJson) ?? throw new InvalidOperationException("PowerToys settings.json is not readable.");
		if (root["enabled"] is not JsonObject map)
		{
			map = new JsonObject();
			root["enabled"] = map;
		}

		map[module] = enabled;
		return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
	}

	/// <summary>The version PowerToys records, without its leading "v".</summary>
	public static string? Version(string generalSettingsJson) =>
		Parse(generalSettingsJson)?["powertoys_version"] is JsonValue value && value.TryGetValue<string>(out var text)
			? text.TrimStart('v', 'V')
			: null;

	private static JsonObject? Parse(string json)
	{
		try
		{
			return JsonNode.Parse(json) as JsonObject;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
