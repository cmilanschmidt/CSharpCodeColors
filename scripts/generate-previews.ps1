<#
.SYNOPSIS
Renders presets\previews\*.png: an excerpt of sample\Sample.cs in VS Code, in each theme as it is (left) and with
its preset applied (right).

.DESCRIPTION
Needs VS Code with the C# extension, and display scaling at 100%. The script runs its own VS Code instance with a
temporary profile (your VS Code settings and open windows are left alone) on a temporary copy of sample\, changes
the theme and the C# color settings through that profile's settings.json, and captures the window, which doesn't
need to be in the foreground. It closes only the VS Code processes it started.

.PARAMETER Preset
Only render these presets (file names without .json, such as dark-modern or visual-studio-2026). Default: all.
#>
[CmdletBinding()]
param(
    [string[]] $Preset
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$presets = Join-Path $repo "presets"
$previews = Join-Path $presets "previews"

# The excerpt of Sample.cs: the type declarations, an extension method, strings, regex and JSON.
$firstLine = 53
$lastLine = 86
$fontSize = 15
$lineHeight = 22

# Every adapted preset is shown next to its theme; Visual Studio's own colors next to VS Code's 2026 themes.
$cases = @()
foreach ($file in Get-ChildItem (Join-Path $presets "adapted") -Filter *.json) {
    $theme = ((Get-Content $file.FullName -Raw | ConvertFrom-Json).'editor.semanticTokenColorCustomizations'.PSObject.Properties | Select-Object -First 1).Name.Trim('[', ']')
    $cases += @{ Preset = $file.BaseName; Name = $file.BaseName; Theme = $theme; Fragment = $file.FullName; Label = "$theme + adapted Visual Studio colors" }
}
foreach ($theme in "Dark 2026", "Light 2026") {
    $kind = $theme.Split(' ')[0].ToLowerInvariant()
    $cases += @{ Preset = "visual-studio-2026"; Name = "visual-studio-2026-$kind"; Theme = $theme; Fragment = (Join-Path $presets "visual-studio-2026.json"); Label = "$theme + Visual Studio 2026 colors" }
}
if ($Preset) { $cases = @($cases | Where-Object { $Preset -contains $_.Preset }) }
if ($cases.Count -eq 0) { throw "No presets to render." }

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class PreviewWindow {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    public static IntPtr Find(string title) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            var text = new StringBuilder(512);
            GetWindowText(h, text, 512);
            if (text.ToString().Contains(title)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@
Add-Type -ReferencedAssemblies System.Drawing @"
using System.Drawing;
public static class PreviewPixels {
    // For each row, whether any pixel between columns x0 and x1 differs from the background.
    public static bool[] RowInk(Bitmap image, Color background, int x0, int x1) {
        var ink = new bool[image.Height];
        int bg = background.ToArgb();
        for (int y = 0; y < image.Height; y++)
            for (int x = x0; x < x1 && !ink[y]; x++)
                ink[y] = image.GetPixel(x, y).ToArgb() != bg;
        return ink;
    }

    // The leftmost column from x0 with a pixel that differs from the background between rows y0 and y1, or -1.
    public static int FirstInkColumn(Bitmap image, Color background, int y0, int y1, int x0, int x1) {
        int bg = background.ToArgb();
        for (int x = x0; x < x1; x++)
            for (int y = y0; y < y1; y++)
                if (image.GetPixel(x, y).ToArgb() != bg) return x;
        return -1;
    }

    // The rightmost column before x1 with a pixel that differs from the background between rows y0 and y1, or -1.
    public static int LastInkColumn(Bitmap image, Color background, int y0, int y1, int x0, int x1) {
        int bg = background.ToArgb();
        for (int x = x1 - 1; x >= x0; x--)
            for (int y = y0; y < y1; y++)
                if (image.GetPixel(x, y).ToArgb() != bg) return x;
        return -1;
    }

    // The first row from startY where consecutive slots of lineHeight rows have text exactly where hasText says, or -1.
    public static int FindLines(bool[] ink, bool[] hasText, int lineHeight, int startY) {
        for (int y = startY; y + hasText.Length * lineHeight <= ink.Length; y++) {
            bool match = true;
            for (int n = 0; n < hasText.Length && match; n++) {
                bool text = false;
                for (int row = y + n * lineHeight + 1; row < y + (n + 1) * lineHeight - 1 && !text; row++) text = ink[row];
                match = text == hasText[n];
            }
            if (match) return y;
        }
        return -1;
    }
}
"@
[PreviewWindow]::SetProcessDPIAware() | Out-Null

# A unique folder name, so the window title tells this instance apart from any other VS Code window.
$work = Join-Path ([IO.Path]::GetTempPath()) "csharpcodecolors-previews"
$folderName = "CSharpCodeColors-preview"
$workspace = Join-Path $work $folderName
$userData = Join-Path $work "user-data"
$title = "Sample.cs - $folderName - Visual Studio Code"

function Stop-PreviewCode {
    Get-CimInstance Win32_Process -Filter "Name = 'Code.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($userData) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

$baseSettings = [ordered]@{
    "security.workspace.trust.enabled" = $false
    "workbench.startupEditor" = "none"
    "workbench.tips.enabled" = $false
    "workbench.editor.showTabs" = "none"
    "workbench.activityBar.location" = "hidden"
    "workbench.statusBar.visible" = $false
    "workbench.secondarySideBar.defaultVisibility" = "hidden"
    "workbench.sideBar.location" = "right"
    # No cursor, squiggles or hint dots in the pictures.
    "workbench.colorCustomizations" = [ordered]@{
        "editorCursor.foreground" = "#00000000"
        "editorError.foreground" = "#00000000"
        "editorWarning.foreground" = "#00000000"
        "editorInfo.foreground" = "#00000000"
        "editorHint.foreground" = "#00000000"
    }
    "window.commandCenter" = $false
    "window.restoreWindows" = "none"
    "extensions.autoUpdate" = $false
    "extensions.autoCheckUpdates" = $false
    "update.mode" = "none"
    "telemetry.telemetryLevel" = "off"
    "chat.disableAIFeatures" = $true
    "breadcrumbs.enabled" = $false
    "editor.fontFamily" = "Cascadia Mono, Consolas, monospace"
    "editor.fontSize" = $fontSize
    "editor.lineHeight" = $lineHeight
    "editor.lineNumbers" = "off"
    "editor.folding" = $false
    "editor.glyphMargin" = $false
    "editor.minimap.enabled" = $false
    "editor.stickyScroll.enabled" = $false
    "editor.codeLens" = $false
    "editor.inlayHints.enabled" = "off"
    "editor.renderLineHighlight" = "none"
    "editor.renderValidationDecorations" = "off"
    "editor.renderWhitespace" = "none"
    "editor.guides.indentation" = $false
    "editor.occurrencesHighlight" = "off"
    "editor.selectionHighlight" = $false
    "editor.lightbulb.enabled" = "off"
    "editor.hover.enabled" = $false
    "editor.overviewRulerLanes" = 0
    "editor.hideCursorInOverviewRuler" = $true
    "editor.scrollbar.vertical" = "hidden"
    "editor.scrollbar.horizontal" = "hidden"
    "editor.cursorBlinking" = "solid"
    "editor.showUnused" = $false
    "editor.showDeprecated" = $false
    "editor.matchBrackets" = "never"
    "editor.guides.bracketPairs" = $false
}

function Set-PreviewSettings([string] $theme, [string] $fragment) {
    $settings = [ordered]@{}
    foreach ($key in $baseSettings.Keys) { $settings[$key] = $baseSettings[$key] }
    $settings["workbench.colorTheme"] = $theme
    if ($fragment) {
        foreach ($property in (Get-Content $fragment -Raw | ConvertFrom-Json).PSObject.Properties) { $settings[$property.Name] = $property.Value }
    }
    $json = $settings | ConvertTo-Json -Depth 30
    [IO.File]::WriteAllText((Join-Path $userData "User\settings.json"), $json, (New-Object Text.UTF8Encoding $false))
}

function Get-WindowImage([IntPtr] $window) {
    $rect = New-Object PreviewWindow+RECT
    [PreviewWindow]::GetWindowRect($window, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [PreviewWindow]::PrintWindow($window, $hdc, 2) | Out-Null
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    return $bitmap
}

function Get-ImageHash([System.Drawing.Bitmap] $bitmap) {
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $hash = [BitConverter]::ToString([Security.Cryptography.MD5]::Create().ComputeHash($stream.ToArray()))
    $stream.Dispose()
    return $hash
}

# Waits until the window shows the same thing twice in a row, after VS Code had at least $minimum seconds.
function Wait-Stable([IntPtr] $window, [int] $minimum) {
    Start-Sleep -Seconds $minimum
    $previous = $null
    for ($i = 0; $i -lt 60; $i++) {
        $image = Get-WindowImage $window
        $hash = Get-ImageHash $image
        if ($hash -eq $previous) { return $image }
        $image.Dispose()
        $previous = $hash
        Start-Sleep -Seconds 2
    }
    throw "VS Code kept changing for two minutes."
}

# Finds the excerpt in a capture: the editor background fills the window's left part below the title bar. The text
# rows tell which lines are on screen: lines 52, 60 and 65 of Sample.cs are blank, so only one scroll position
# (and pixel offset, since VS Code may scroll by part of a line) matches. Returns the excerpt's top-left corner.
function Find-Excerpt([System.Drawing.Bitmap] $image) {
    $background = $image.GetPixel(16, [int]($image.Height / 2))
    $editorTop = 0
    while ($editorTop -lt $image.Height -and $image.GetPixel(16, $editorTop) -ne $background) { $editorTop++ }
    $ink = [PreviewPixels]::RowInk($image, $background, 16, 1000)
    # Lines around the excerpt: text where the file has text, nothing where it is blank.
    $lines = Get-Content (Join-Path $workspace "Sample.cs")
    $from = $firstLine - 1; $to = $lastLine + 3
    [bool[]] $hasText = @(for ($n = $from; $n -le $to; $n++) { $lines[$n - 1].Trim().Length -gt 0 })
    $y = [PreviewPixels]::FindLines($ink, $hasText, $lineHeight, $editorTop)
    if ($y -lt 0) {
        $capture = Join-Path $work "unrecognized-capture.png"
        $image.Save($capture, [System.Drawing.Imaging.ImageFormat]::Png)
        throw "Can't find lines $firstLine-$lastLine of Sample.cs in the VS Code window; see $capture."
    }
    $top = $y + ($firstLine - $from) * $lineHeight
    $left = [PreviewPixels]::FirstInkColumn($image, $background, $top, $top + ($lastLine - $firstLine + 1) * $lineHeight, 16, 200)
    $right = [PreviewPixels]::LastInkColumn($image, $background, $top, $top + ($lastLine - $firstLine + 1) * $lineHeight, $left, 1250)
    return @{ Top = $top; Left = $left; Right = $right; Background = $background }
}

function New-Preview([System.Drawing.Bitmap] $before, [System.Drawing.Bitmap] $after, [hashtable] $case, [string] $path) {
    $codeHeight = ($lastLine - $firstLine + 1) * $lineHeight
    $panels = @(@{ Image = $before; Label = $case.Theme }, @{ Image = $after; Label = $case.Label })
    foreach ($panel in $panels) { $panel.Excerpt = Find-Excerpt $panel.Image }
    # As wide as the longest line of the excerpt in either capture.
    $codeWidth = [int]($panels | ForEach-Object { $_.Excerpt.Right - $_.Excerpt.Left + 2 } | Measure-Object -Maximum).Maximum
    $pad = 24; $header = 44; $gap = 2
    $panelWidth = $codeWidth + 2 * $pad
    $panelHeight = $header + $codeHeight + $pad
    $canvas = New-Object System.Drawing.Bitmap (2 * $panelWidth + $gap), $panelHeight
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $labelFont = New-Object System.Drawing.Font "Segoe UI Semibold", 14, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.Clear([System.Drawing.Color]::FromArgb(128, 128, 128))
    $i = 0
    foreach ($panel in $panels) {
        $excerpt = $panel.Excerpt
        $left = $i * ($panelWidth + $gap)
        $background = New-Object System.Drawing.SolidBrush $excerpt.Background
        $g.FillRectangle($background, $left, 0, $panelWidth, $panelHeight)
        $source = New-Object System.Drawing.Rectangle ($excerpt.Left - 1), $excerpt.Top, $codeWidth, $codeHeight
        $g.DrawImage($panel.Image, (New-Object System.Drawing.Rectangle ($left + $pad), $header, $codeWidth, $codeHeight), $source, [System.Drawing.GraphicsUnit]::Pixel)
        $b = $excerpt.Background
        $ink = if ((0.2126 * $b.R + 0.7152 * $b.G + 0.0722 * $b.B) -lt 128) { [System.Drawing.Color]::FromArgb(170, 255, 255, 255) } else { [System.Drawing.Color]::FromArgb(150, 0, 0, 0) }
        $brush = New-Object System.Drawing.SolidBrush $ink
        $g.DrawString($panel.Label, $labelFont, $brush, $left + $pad - 2, 13)
        $line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, $ink.R, $ink.G, $ink.B)), 1
        $g.DrawLine($line, $left + $pad, $header - 8, $left + $panelWidth - $pad, $header - 8)
        $brush.Dispose(); $background.Dispose(); $line.Dispose()
        $i++
    }
    $canvas.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $labelFont.Dispose(); $canvas.Dispose()
}

Stop-PreviewCode
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force (Join-Path $userData "User"), $previews | Out-Null
# The sample project's own files only (not bin, obj or .vs, which may be in use).
New-Item -ItemType Directory -Force $workspace | Out-Null
Get-ChildItem (Join-Path $repo "sample") -File | Copy-Item -Destination $workspace

Set-PreviewSettings $cases[0].Theme $null
$code = Join-Path $env:LOCALAPPDATA "Programs\Microsoft VS Code\Code.exe"
if (-not (Test-Path $code)) { $code = (Get-Command code -ErrorAction Stop).Source -replace '\\bin\\code(\.cmd)?$', '\Code.exe' }
$extensions = Join-Path $env:USERPROFILE ".vscode\extensions"
# Centering line 70 puts lines 53-86 on screen; C# Dev Kit is off (its walkthroughs would cover the file).
Start-Process $code -ArgumentList @("--user-data-dir", "`"$userData`"", "--extensions-dir", "`"$extensions`"",
    "--disable-extension", "ms-dotnettools.csdevkit", "--new-window", "`"$workspace`"", "--goto", "`"$(Join-Path $workspace 'Sample.cs'):70`"")
try {
    $window = [IntPtr]::Zero
    for ($i = 0; $i -lt 60 -and $window -eq [IntPtr]::Zero; $i++) { Start-Sleep -Seconds 1; $window = [PreviewWindow]::Find($title) }
    if ($window -eq [IntPtr]::Zero) { throw "VS Code didn't open a window titled ""$title""." }
    [PreviewWindow]::ShowWindow($window, 9) | Out-Null
    [PreviewWindow]::MoveWindow($window, 0, 0, 1600, 1060, $true) | Out-Null
    # The C# extension needs a while to load the project before it colors the code semantically.
    Wait-Stable $window 45 | ForEach-Object { $_.Dispose() }

    foreach ($case in $cases) {
        Set-PreviewSettings $case.Theme $null
        $before = Wait-Stable $window 4
        Set-PreviewSettings $case.Theme $case.Fragment
        $after = Wait-Stable $window 4
        $path = Join-Path $previews "$($case.Name).png"
        New-Preview $before $after $case $path
        $before.Dispose(); $after.Dispose()
        Write-Host "Wrote previews\$($case.Name).png"
    }
}
finally {
    Stop-PreviewCode
}
Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
