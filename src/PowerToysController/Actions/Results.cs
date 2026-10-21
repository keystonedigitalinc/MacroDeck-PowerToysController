using KeystoneDigital.PowerToysController.State;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace KeystoneDigital.PowerToysController.Actions;

/// <summary>The checks every action makes before it touches PowerToys, and the shared failures.</summary>
internal static class Results
{
	/// <summary>A failure when PowerToys cannot be reached right now, else null.</summary>
	public static ActionResult? Gate(PowerToysSnapshot snapshot) => snapshot.Status switch
	{
		"ready" => null,
		"not_installed" => ActionResult.Failed(ActionErrorCodes.NotConfigured, Strings.Errors.NotInstalled()),
		"elevated" => ActionResult.Failed(ActionErrorCodes.PermissionDenied, Strings.Errors.Elevated()),
		"restarting" => ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.Restarting()),
		_ => ActionResult.Failed(ActionErrorCodes.NotConnected, Strings.Errors.NotRunning()),
	};

	public static ActionResult NotApplied() => ActionResult.Failed(ActionErrorCodes.Timeout, Strings.Errors.NotApplied());

	public static ActionResult Invalid(LocalizedText fieldLabel) =>
		ActionResult.Failed(ActionErrorCodes.InvalidParameter, MacroDeckStrings.Validation.Required(fieldLabel));

	public static ActionParameterOption Option(string value, LocalizedText label) => new() { Value = value, Label = label };
}
