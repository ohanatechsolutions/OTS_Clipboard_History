# Security and privacy

OTS Clipboard is a local-only tool. No accounts, analytics, cloud services, or networking are used.

- New plain-text clipboard copies are saved for the active Windows user in DPAPI-encrypted form at:
  %LOCALAPPDATA%\OhanaTechSolutions\OTSClipboard\history.dat
- Encryption is scoped to the current Windows user; other processes running as that user can potentially access clipboard text while it is live.
- Windows clipboard history, remote desktop clipboard syncing, other clipboard tools, crash dumps, filesystem backups, and applications may create their own copies. Clearing this app's history does not clear those copies.
- No passwords, payment card numbers, or other sensitive text are filtered automatically. If you use password managers or sensitive documents, pause tracking whenever appropriate.
- Deleting a history item rewrites the encrypted history file; this is not guaranteed to securely erase previously allocated SSD storage or backups.
- Nothing begins recording until the app is running; it cannot recover copies made before it started.
- The Windows registry autostart option is OFF by default.
- The application closes to the notification tray to continue monitoring. Right-click the tray icon and select Exit to stop monitoring.
- No updates are downloaded automatically.
- No credentials are required for building or installing.

To report a security problem, create a private report to the repository owner instead of pasting credentials into a public issue.
