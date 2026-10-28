# OTS Clipboard History branding

This folder contains reusable raster logo artwork for Ohana Tech Solutions' OTS Clipboard History application.

- `OTS_Clipboard_History_Logo.png`: original, 1024×1024 transparent PNG supplied by the user, with no resizing or palette conversion.
- `../../src/OTS.Clipboard.History/Resources/App.ico`: original Windows icon embedded in the executable and installer.

**Use:** ICO for Windows app identity (installer, executable, tray, shortcuts). PNG for documentation, preview images, and the application header (embedded with MSBuild as `Resources/BrandLogo.png`).

**Quality:** The PNG is the original 1024×1024 RGBA asset. The supplied ICO is 32×32, appropriate for the executable, tray, and shortcuts. Generate a multi-size ICO from the original PNG in a future update if a broader range of Windows display sizes is needed.

Keep both formats visually consistent; do not use the older watermarked icon.
