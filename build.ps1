param(
    [string]$OutputDirectory = "$PSScriptRoot\dist\v0.20",
    [switch]$Installer,
    [string]$InnoCompiler
)
$ErrorActionPreference = 'Stop'
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$appIconPath = "$PSScriptRoot\assets\app.ico"
if (!(Test-Path -LiteralPath $appIconPath) -or (Get-Item "$PSScriptRoot\build-icon.ps1").LastWriteTimeUtc -gt (Get-Item -LiteralPath $appIconPath).LastWriteTimeUtc) { & "$PSScriptRoot\build-icon.ps1" }
$iconResources = Get-ChildItem "$PSScriptRoot\assets\icons\*.svg" | ForEach-Object { "/resource:$($_.FullName),Icons.$($_.Name)" }
$fontResources = Join-Path $OutputDirectory 'lk_Screenshot.g.resources'
$resourceWriter = New-Object System.Resources.ResourceWriter($fontResources)
try {
    $font = [IO.File]::OpenRead("$PSScriptRoot\assets\fonts\RobotoFlex.ttf")
    $resourceWriter.AddResource('fonts/robotoflex.ttf', $font, $true)
    $resourceWriter.AddResource('fonts/ofl.txt', [IO.File]::ReadAllText("$PSScriptRoot\assets\fonts\OFL.txt"))
} finally { $resourceWriter.Dispose() }
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /utf8output /win32icon:"$PSScriptRoot\assets\app.ico" /out:"$OutputDirectory\lk_Screenshot.exe" /reference:"$framework\WPF\PresentationCore.dll" /reference:"$framework\WPF\PresentationFramework.dll" /reference:"$framework\WPF\WindowsBase.dll" /reference:System.Xaml.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /resource:"$PSScriptRoot\src\Theme.xaml,Theme.xaml" /resource:"$PSScriptRoot\assets\app.ico,AppIcon.ico" /resource:"$fontResources",lk_Screenshot.g.resources $iconResources "$PSScriptRoot\src\App.cs" "$PSScriptRoot\src\ImageDocument.cs" "$PSScriptRoot\src\CropOverlay.cs" "$PSScriptRoot\src\CursorCapture.cs" "$PSScriptRoot\src\Hotkey.cs" "$PSScriptRoot\src\InlineText.cs" "$PSScriptRoot\src\TrayMenu.cs" "$PSScriptRoot\src\Theme.cs" "$PSScriptRoot\src\ToolIcons.cs" "$PSScriptRoot\src\AssemblyInfo.cs"
$buildExitCode = $LASTEXITCODE
Remove-Item -LiteralPath $fontResources
if ($buildExitCode -ne 0) { throw 'Build failed' }
Copy-Item "$PSScriptRoot\src\App.config" "$OutputDirectory\lk_Screenshot.exe.config" -Force
$releasePath = (Resolve-Path -LiteralPath $OutputDirectory).Path
$exePath = Join-Path $releasePath 'lk_Screenshot.exe'
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut((Join-Path $releasePath 'lk_Screenshot.lnk'))
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $releasePath
$shortcut.IconLocation = "$exePath,0"
$shortcut.Description = 'lk_Screenshot'
$shortcut.Save()
Get-Item "$OutputDirectory\lk_Screenshot.exe" | Select-Object FullName,Length

if ($Installer) {
    if (!$InnoCompiler) {
        $compiler = Get-Command ISCC.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($compiler) { $InnoCompiler = $compiler.Source }
        else { $InnoCompiler = "$PSScriptRoot\..\lk_PieMenu\tools\inno\ISCC.exe" }
    }
    if (!(Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
        throw 'Inno Setup compiler not found. Pass -InnoCompiler with the path to ISCC.exe.'
    }
    $InnoCompiler = (Resolve-Path -LiteralPath $InnoCompiler).Path
    $version = ([version](Get-Item -LiteralPath $exePath).VersionInfo.FileVersion).ToString(3)
    & $InnoCompiler /Qp "/DAppVersion=$version" "/DAppExe=$exePath" "/O$releasePath" "$PSScriptRoot\installer.iss"
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    Get-Item -LiteralPath (Join-Path $releasePath 'lk_Screenshot-Setup.exe') | Select-Object FullName,Length
}
