# Windows build status

The Windows desktop app is implemented in `Windows/` and builds with .NET 10 / WPF.
The standalone distribution includes its .NET runtime; it does not require a paid service or Apple tooling.

Local checks cover portable ink and pressure data, unknown-field preservation, atomic save/recovery,
backup restore, unsafe ZIP rejection, unsupported schema/pressure rejection, DPAPI-protected OAuth configuration,
revision/conflict handling for the portable format, PDF export and native Windows PDF render/import/export.
The PDF import test detected and fixed Windows DIP versus PDF point coordinates.

An interactive test in an isolated profile drew a real stroke, verified its autosave, removed it with Undo,
and restored it with Redo. OAuth was launched from the actual Windows app into Google's system-browser login page.
These checks do not establish hardware pen quality, microphone/camera behavior, or full iPad feature parity.

iCloud is excluded from the Windows UI at the user's request.

Google OAuth and Drive backup upload/download are implemented, but a successful account connection and
upload/restore roundtrip have not yet been verified. A dedicated Google project is awaiting user approval.
The existing Google project has a consent application named LiftLog and must not be relabeled as OwlLuna.
No Google configuration, client values or tokens belong in this public source repository.

See README.md for implemented capabilities and remaining differences from the iPad app.
