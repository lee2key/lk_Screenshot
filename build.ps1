param([string]$OutputDirectory = "$PSScriptRoot\dist\v0.16")
$ErrorActionPreference = 'Stop'
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$appIconPath = "$PSScriptRoot\assets\app.ico"
if (!(Test-Path -LiteralPath $appIconPath) -or (Get-Item "$PSScriptRoot\build-icon.ps1").LastWriteTimeUtc -gt (Get-Item -LiteralPath $appIconPath).LastWriteTimeUtc) { & "$PSScriptRoot\build-icon.ps1" }
$iconResources = Get-ChildItem "$PSScriptRoot\assets\icons\*.svg" | ForEach-Object { "/resource:$($_.FullName),Icons.$($_.Name)" }
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /utf8output /win32icon:"$PSScriptRoot\assets\app.ico" /out:"$OutputDirectory\lk Screenshot.exe" /reference:"$framework\WPF\PresentationCore.dll" /reference:"$framework\WPF\PresentationFramework.dll" /reference:"$framework\WPF\WindowsBase.dll" /reference:System.Xaml.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /resource:"$PSScriptRoot\src\Theme.xaml,Theme.xaml" /resource:"$PSScriptRoot\assets\app.ico,AppIcon.ico" $iconResources "$PSScriptRoot\src\App.cs" "$PSScriptRoot\src\ImageDocument.cs" "$PSScriptRoot\src\CropOverlay.cs" "$PSScriptRoot\src\CursorCapture.cs" "$PSScriptRoot\src\Theme.cs" "$PSScriptRoot\src\ToolIcons.cs" "$PSScriptRoot\src\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item "$PSScriptRoot\src\App.config" "$OutputDirectory\lk Screenshot.exe.config" -Force
$releasePath = (Resolve-Path -LiteralPath $OutputDirectory).Path
$exePath = Join-Path $releasePath 'lk Screenshot.exe'
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut((Join-Path $releasePath 'lk Screenshot.lnk'))
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $releasePath
$shortcut.IconLocation = "$exePath,0"
$shortcut.Description = 'lk Screenshot'
$shortcut.Save()
Get-Item "$OutputDirectory\lk Screenshot.exe" | Select-Object FullName,Length
