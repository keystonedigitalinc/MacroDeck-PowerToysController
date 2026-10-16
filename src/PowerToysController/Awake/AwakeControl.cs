using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KeystoneDigital.PowerToysController.IO;
using KeystoneDigital.PowerToysController.Locator;

namespace KeystoneDigital.PowerToysController.Awake;

internal enum AwakeMode
{
	Off = 0,
	Indefinite = 1,
	Timed = 2,
	Until = 3,
}

internal sealed record AwakeState(AwakeMode Mode, bool KeepDisplayOn, int Hours, int Minutes, DateTimeOffset? Until);

/// <summary>
/// Awake watches its own settings.json and applies a change within milliseconds; PowerToys' Awake
/// service sets modes the same way. Every field this class does not own is written back untouched.
/// </summary>
internal sealed class AwakeControl(IFileStore files, PowerToysPaths paths, TimeProvider time) : IDisposable
{
	private static readonly TimeSpan RetryFor = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan RetryEvery = TimeSpan.FromMilliseconds(50);

	private readonly SemaphoreSlim _gate = new(1, 1);

	public void Dispose() => _gate.Dispose();

	public AwakeState? Read()
	{
		try
		{
			return Parse(files.Read(paths.AwakeSettings)) is { } root ? StateOf(root) : null;
		}
		catch (IOException)
		{
			return null;
		}
	}

	public async Task<bool> SetAsync(AwakeMode mode, bool keepDisplayOn, int minutes, DateTimeOffset? until, CancellationToken ct)
	{
		await _gate.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			var root = await ReadWithRetryAsync(ct).ConfigureAwait(false);
			if (root is null) return false;

			var properties = root["properties"]!.AsObject();
			properties["mode"] = (int)mode;
			properties["keepDisplayOn"] = keepDisplayOn;
			if (mode == AwakeMode.Timed)
			{
				var total = Math.Max(1, minutes);
				properties["intervalHours"] = total / 60;
				properties["intervalMinutes"] = total % 60;
			}

			if (mode == AwakeMode.Until && until is { } expiry)
			{
				properties["expirationDateTime"] = expiry.ToString("o", CultureInfo.InvariantCulture);
			}

			var expected = StateOf(root);
			await WriteWithRetryAsync(root.ToJsonString(), ct).ConfigureAwait(false);

			return await ReadWithRetryAsync(ct).ConfigureAwait(false) is { } after && StateOf(after) == expected;
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task<JsonObject?> ReadWithRetryAsync(CancellationToken ct)
	{
		var deadline = time.GetUtcNow() + RetryFor;

		while (true)
		{
			try
			{
				var text = files.Read(paths.AwakeSettings);
				if (text is null) return null;
				if (Parse(text) is { } root) return root;
			}
			catch (IOException)
			{
				// Awake or PowerToys is writing the file right now.
			}

			if (time.GetUtcNow() >= deadline) return null;
			await Task.Delay(RetryEvery, ct).ConfigureAwait(false);
		}
	}

	private async Task WriteWithRetryAsync(string content, CancellationToken ct)
	{
		var deadline = time.GetUtcNow() + RetryFor;

		while (true)
		{
			try
			{
				files.Write(paths.AwakeSettings, content);
				return;
			}
			catch (IOException) when (time.GetUtcNow() < deadline)
			{
				await Task.Delay(RetryEvery, ct).ConfigureAwait(false);
			}
		}
	}

	private static JsonObject? Parse(string? text)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;

		try
		{
			return JsonNode.Parse(text) is JsonObject root && root["properties"] is JsonObject ? root : null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static AwakeState StateOf(JsonObject root)
	{
		var properties = root["properties"]!.AsObject();
		var until = properties["expirationDateTime"] is JsonValue value && value.TryGetValue<string>(out var text)
			&& DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
				? parsed
				: (DateTimeOffset?)null;

		return new AwakeState(
			(AwakeMode)Int(properties, "mode"),
			properties["keepDisplayOn"] is JsonValue display && display.TryGetValue<bool>(out var on) && on,
			Int(properties, "intervalHours"),
			Int(properties, "intervalMinutes"),
			until);
	}

	private static int Int(JsonObject properties, string name) =>
		properties[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
}
