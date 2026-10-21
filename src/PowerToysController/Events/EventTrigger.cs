namespace KeystoneDigital.PowerToysController.Events;

internal enum TriggerResult
{
	Triggered,
	NotRunning,
	Denied,
}

internal interface INamedEventOpener
{
	TriggerResult TrySet(string name);
}

/// <summary>
/// Opens an existing event and sets it. It never creates one: a missing event means the module's listener
/// is not running, and a stray event made here would hide that.
/// </summary>
internal sealed class WindowsNamedEventOpener : INamedEventOpener
{
	public TriggerResult TrySet(string name)
	{
		try
		{
			if (!EventWaitHandle.TryOpenExisting(name, out var handle)) return TriggerResult.NotRunning;

			using (handle)
			{
				handle.Set();
				return TriggerResult.Triggered;
			}
		}
		catch (UnauthorizedAccessException)
		{
			// The event belongs to an elevated PowerToys.
			return TriggerResult.Denied;
		}
	}
}

/// <summary>Opens the event on every press, so a PowerToys restart or update needs no plugin restart.</summary>
internal sealed class EventTrigger(INamedEventOpener opener)
{
	public TriggerResult Trigger(PowerToysTool tool) => opener.TrySet(PowerToysEvents.NameOf(tool));
}
