# Installable Windows application

The **real** Windows installation file is named **OTS_Clipboard_Setup.exe** (not an .iss script).

After the Windows GitHub Actions workflow succeeds, it commits the real installer and its matching SHA-256 file to this folder, provided the installer is below GitHub's per-file size limit.

If the file is not present, go to **Actions > Build Windows installer > latest successful run > Artifacts > OTS_Clipboard_Windows_Installer**, download the ZIP, extract it, and double-click **OTS_Clipboard_Setup.exe**.

To verify integrity in Windows PowerShell:

    Get-FileHash .\OTS_Clipboard_Setup.exe -Algorithm SHA256

Compare the output to the hex string in **OTS_Clipboard_Setup.exe.sha256**. Windows SmartScreen can warn about unsigned, newly created installers; the app is not code-signed.

The .iss file in the packaging directory is the *installer source code*, not an installer.
