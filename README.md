# lk_Screenshot

A Windows utility for capturing, annotating, and saving screenshots as PNG files. It runs locally and stays in the system tray.

## Installation

Download **lk_Screenshot-Setup.exe** from the [latest release](https://github.com/lee2key/lk_Screenshot/releases/latest). The app requires Windows 11 x64 or Windows 10 x64 version 1903 or later, with .NET Framework 4.8 or newer.

The installer runs without administrator rights. It adds a Start menu shortcut and offers an optional desktop shortcut. If an earlier version is running, right-click its tray icon and select **Exit** before installing.

## Usage

| Default shortcut | Action |
| --- | --- |
| Print Screen | Select an area of the primary monitor |
| Shift + Print Screen | Capture the entire primary monitor |
| Ctrl + Print Screen | Open the screenshots folder |
| Ctrl+S or Ctrl+C in the editor | Save the PNG and copy the screenshot to the clipboard |
| Ctrl+Z | Undo an edit |
| Esc | Cancel text editing or cropping, or close the editor |

- In **Settings**, click a shortcut field, press the new key combination, and click **Save**. You can also choose where screenshots are saved, whether the editor opens after capture, and whether the app starts with Windows.
- Screenshots include the visible mouse pointer. The filename uses the time you pressed the capture shortcut, as in `Screenshot_2026-11-28_02-15.png`.
- The editor includes Pencil, Rectangle, Arrow, Text, Blur, Mosaic, and Crop. Drag the crop frame to adjust it, and use the mouse wheel to zoom around the pointer.
- With **Text** selected, double-click an empty area to add a caption or double-click an existing one to edit it. Press Enter for a new line. Click an empty area to finish editing, then drag the caption to move it. While typing, Ctrl+C copies selected text and Ctrl+S saves the screenshot.
- If Windows Snipping Tool or another app uses Print Screen, choose an available shortcut in **Settings**.

## Build and test

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
& '.\dist\v0.20\lk_Screenshot.exe' --self-test test-output
```

Add `-Installer` to build the installer. The script looks for the Inno Setup 6 compiler (`ISCC.exe`) on PATH and in the sibling `lk_PieMenu` project. You can also specify its path with `-InnoCompiler`. Build output is written to `dist/v0.20`, and the self-test report is saved to `test-output/result.txt`.

To try the editor without capturing the screen, run the executable with `--editor-demo`. This mode saves the result as `demo-result.png`.

The bundled Roboto Flex font is distributed under the [SIL Open Font License](assets/fonts/OFL.txt).
