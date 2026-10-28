# OTS Clipboard History

**OTS Clipboard History** is a lightweight, native Windows text clipboard-history app, developed by **Ohana Tech Solutions**. It silently monitors Windows clipboard changes while running, shows each copied text with a local timestamp, and lets you click any entry to copy it again.

## Install (Windows 10 or 11, x64)

1. Open the **installer** folder in this GitHub repository.
2. Download **OTS_Clipboard_History_Setup.exe** (this is the actual installer).
3. Double-click the downloaded .exe, follow the setup wizard, and select **Launch OTS Clipboard History**.
4. The program opens immediately and records future plain-text copies. Try copying a sentence using Ctrl+C or the right-click Copy menu.
5. Optionally check **Start with Windows** in the app so tracking begins after you sign in.

If upgrading from the earlier **OTS Clipboard** application, close it using its tray icon before installing. The setup wizard uses the same Inno Setup application ID, and on first run the new app migrates the legacy DPAPI-encrypted history to the new data directory without deleting the original file. If **Start with Windows** was previously enabled, the app updates that registry entry to the renamed executable. You can delete the legacy history folder after verifying the new history is complete.

If the installer has not appeared yet, open **Actions > Build Windows installer > the newest successful run**, download the **OTS_Clipboard_History_Windows_Installer** artifact, unzip it, and run the setup .exe. The workflow builds and saves the real .exe; **packaging/OTS_Clipboard_History.iss** is only installer *source*. Installers are unsigned, so SmartScreen may ask for confirmation.

**The installer bundles the .NET Desktop runtime** (self-contained). Users don't need Python, Visual Studio, or the .NET SDK.

## Application logo assets

![OTS Clipboard History logo](assets/branding/OTS_Clipboard_History_Logo.png)

Both formats of the same watermark-free artwork are available in this repository. The original 1024px PNG is embedded as the branding image in the app header, while the ICO continues to identify the Windows executable, installer, taskbar, system tray, and shortcuts:

- **ICO:** `src/OTS.Clipboard.History/Resources/App.ico` — embedded into the Windows executable and used for the taskbar, title bar, system tray, shortcuts, and installer.
- **PNG:** `assets/branding/OTS_Clipboard_History_Logo.png` — original 1024×1024 transparent PNG for README, documentation, large UI graphics, and promotional images.

The official PNG is now the user's original **1024 × 1024 transparent image**, retained at full resolution without resizing, recompression, or watermarking. The Windows ICO is the exact user-supplied **32 × 32** icon. Both depict the same artwork and serve different use cases.

The app uses the supplied clipboard-list artwork, stored at `src/OTS.Clipboard.History/Resources/App.ico`. This icon is embedded into the Windows executable and used by the main window, edit dialog, system tray, installer, and Start menu/desktop shortcuts. Reinstall `OTS_Clipboard_History_Setup.exe` to apply the update. If Windows continues displaying an older icon due to its icon cache, unpin and re-pin the shortcut or restart Windows Explorer.

## New v1.2.2 interface and tray interaction

- **Updated custom icon:** the supplied watermark-free \`clipboard_transparent.ico\` is embedded as \`Resources/App.ico\` and replaces the previous icon in the main window, edit window, compiled executable, installer, shortcuts and system tray.
- **System tray toggle:** one **left click** on the tray icon shows the window when hidden and hides it when visible. Right-click opens the Show / Hide and Exit menu. The icon stays present while the application runs; Windows may place it under the hidden-icons **^** overflow. Pin it from Windows **Settings > Personalization > Taskbar > Other system tray icons** to make it always visible.
- **Polished dark mode:** glass-like rounded slate clip cards, softer steel-blue selection glow, more breathing room and text padding, search field and clip category filtering.
- **Readable dark category dropdown:** both the closed filter and its expanded options use dark slate panels with high-contrast white text and blue hover/selected states.
- **Visual multi-select:** every card has a checkbox; checked cards show a vivid steel-blue highlight and a checkmark, and the toolbar displays how many clips are selected. Checking a box adds or removes that card without losing other selections or copying the clip. Ctrl/Shift row selection, right-click selection actions, and Delete selected continue to work.
- **Images supported:** stores actual clipboard images as PNG inside the encrypted local history, limited to 5 MB per image. Clicking an image history entry restores the image to Windows clipboard for pasting. Text, Links, Code, and hex Color clips are identified automatically.
- **Search:** filter history by text or clip category (image-only clips can be filtered with the Images dropdown).
- **Existing history preserved:** older encrypted text-only entries remain readable. Nothing is uploaded to the cloud.

## Using the app

- **Copy text anywhere in Windows:** the app automatically records it with a local timestamp.
- **Left-click a row:** the exact full text is placed on the Windows clipboard for Ctrl+V.
- **Right-click a row:** Copy, Edit, Select / Deselect, Select All, Deselect All, Delete.
- **Ctrl-click / Shift-click:** select multiple rows. Delete selected removes all highlighted rows.
- **Clear history ▾:** delete items from the last 15m, 30m, 1–6h, 12h, 24h, 48h, 72h, 1 week, 1 month, 1 year, or all time. A confirmation appears before clearing.
- **Track clipboard:** pause/resume collection. The app ignores consecutive identical copies.
- **Start with Windows:** optional per-user autostart, disabled by default.
- **Minimize or close the window:** it keeps working in the notification tray; double-click the tray icon to reopen. Right-click the tray icon > **Exit OTS Clipboard History** to stop it.

Plain text and images are captured (image PNG payload up to 5 MB); rich text formatting and file-list copies are not captured. To keep the app responsive it skips clipboard text longer than two million characters. Editing a saved entry does not modify the current system clipboard until you copy that entry again. UI uses a one-line preview; clicking copies the complete original text.

## Security / data storage

History is automatically persisted locally, encrypted using Windows DPAPI in the current user account. Data file: %LOCALAPPDATA%\OhanaTechSolutions\OTSClipboardHistory\history.dat. The app makes no network requests. See [SECURITY.md](SECURITY.md) for important privacy limitations: copied passwords and other secrets are still sensitive, and clearing app history does not erase Windows' or other apps' clipboard logs.

Uninstall via Windows **Settings > Apps > Installed apps > OTS Clipboard History**. Your encrypted history data is intentionally not deleted automatically on uninstall. To remove it as well, first exit the app and then delete the folder %LOCALAPPDATA%\OhanaTechSolutions\OTSClipboardHistory.

## Build from source (step by step)

Requirements: Windows 10/11 x64, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (free) and [Inno Setup 6](https://jrsoftware.org/isinfo.php) (free).

1. On GitHub, click **Code > Download ZIP**, then extract the project somewhere on Windows.
2. Install the .NET 10 SDK, then install Inno Setup 6 using its normal installation wizard.
3. Open the extracted repository folder in File Explorer.
4. Click the folder address bar, type **powershell**, and press Enter.
5. In the PowerShell window, run this command:

        powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1

6. The actual installer will appear at **dist\setup\OTS_Clipboard_History_Setup.exe**. Its SHA-256 checksum is in **dist\setup\OTS_Clipboard_History_Setup.exe.sha256**.
7. Double-click the setup .exe to install and launch the program.

To run from source without an installer, in the project folder run:

    dotnet run --project src/OTS.Clipboard.History/OTS.Clipboard.History.csproj

## Automated GitHub build

The workflow at **.github/workflows/windows-installer.yml** runs on every push to main (and via **Actions > Build Windows installer > Run workflow**). A Windows runner:
1. Restores/builds the C# WPF project.
2. Publishes a self-contained single-file win-x64 app.
3. Compiles an Inno Setup installer executable.
4. Generates the matching SHA-256 checksum.
5. Publishes a downloadable GitHub Actions artifact.
6. Commits installer .exe and .sha256 to **installer/** when below GitHub's file-size limit.

GitHub Actions availability and minutes depend on your GitHub plan, especially for private repositories. This CI is the Windows build and packaging check; GUI interaction and tray behavior should also be smoke-tested on an actual Windows desktop.

## Source layout

- **src/OTS.Clipboard.History/** — WPF desktop app and encrypted history persistence.
- **packaging/OTS_Clipboard_History.iss** — Inno Setup installer definition.
- **scripts/build-installer.ps1** — one-command Windows build.
- **installer/** — downloadable executable once CI finishes.
- **.github/workflows/** — automated Windows installer build.
- **SECURITY.md** — private data and threat-model notes.

## Requirements, known limits, and troubleshooting

- Windows 10/11 x64 only, normal desktop user session. No administrator privileges required.
- Supports Unicode text clipboard formats. Apps that block clipboard reads or Windows secure desktops cannot be monitored.
- No global hotkey; left-click the system tray icon to show or hide the app.
- Multiple instances are prevented; if the app says it is already running, find the OTS Clipboard History tray icon.
- If new entries do not appear, verify **Track clipboard** is checked and the app is running in the tray.
- If you can't find the app after closing it, open the system tray / hidden icons area. You may need to enable the OTS Clipboard History icon in Windows Taskbar settings; apps cannot force Windows to pin tray icons.
- If encrypted history can't be decrypted after Windows user-profile migration, it won't be silently overwritten. Keep the file safe and use the original Windows account/DPAPI keys to recover access.
- If a Windows build fails, inspect **GitHub > Actions > Build Windows installer > the failed run** and review its logs.
- Installer uses **per-user installation** and **does not require elevation**.
- Application and installer are not Authenticode code-signed.

## Version and licensing

Version **1.2.2**, 2026. Developed by **Ohana Tech Solutions**. This private repository has no open-source license grant; ask the owner before redistributing source code or compiled binaries.
