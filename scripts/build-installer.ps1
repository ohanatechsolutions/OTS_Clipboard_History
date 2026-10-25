# Build OTS Clipboard app and real Windows installer on any Windows 10/11 x64 PC.
# Prerequisites: .NET 10 SDK and Inno Setup 6.
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '..'))

dotnet restore 'src/OTS.Clipboard/OTS.Clipboard.csproj'
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }

dotnet publish 'src/OTS.Clipboard/OTS.Clipboard.csproj' --no-restore -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o 'dist/publish'
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

$iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { throw 'Install Inno Setup 6 first (https://jrsoftware.org/isinfo.php).' }
& $iscc 'packaging/OTS_Clipboard.iss'
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }

$file = 'dist/setup/OTS_Clipboard_Setup.exe'
$hash = (Get-FileHash -Algorithm SHA256 $file).Hash.ToLowerInvariant()
Set-Content 'dist/setup/OTS_Clipboard_Setup.exe.sha256' "$hash  OTS_Clipboard_Setup.exe" -Encoding ascii
Write-Host "Installer ready: $(Resolve-Path $file)"
Write-Host "SHA-256: $hash"
