<#
.SYNOPSIS
Regenerates every file in presets\: Visual Studio's own colors (visual-studio-2026.json) and the colors adapted
to each built-in VS Code theme (adapted\*.json).

.DESCRIPTION
Needs the .NET 10 SDK, VS Code, and exactly one running Visual Studio 2026 (with a C# file opened once, so
its C# colors are loaded). The script switches Visual Studio to its Dark theme, then to its Light theme, by
editing Visual Studio's settings.json, and restores that file exactly as it was when done, even on failure.

.PARAMETER OutputDirectory
Where to write the presets. Default: the repository's presets folder.
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot "..\presets")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

# VS Code theme names (as CSharpCodeColors --themes lists them), adapted from VS Dark and VS Light respectively.
$darkThemes = "Dark Modern", "Dark+", "Dark 2026", "Visual Studio Dark", "Monokai", "Monokai Dimmed",
    "Solarized Dark", "Abyss", "Kimbie Dark", "Red", "Tomorrow Night Blue"
$lightThemes = "Light Modern", "Light+", "Light 2026", "Visual Studio Light", "Solarized Light", "Quiet Light"

function Get-PresetName([string] $theme) {
    # "Dark Modern" -> dark-modern, "Dark+" -> dark-plus
    return $theme.ToLowerInvariant().Replace("+", "-plus").Replace(" ", "-")
}

function ConvertTo-JsonString([string] $value) {
    if ($null -eq $value) { return "null" }
    return '"' + $value.Replace('\', '\\').Replace('"', '\"') + '"'
}

# The settings.json of the running Visual Studio 2026 instance (its "unified settings").
function Get-VisualStudioSettingsPath {
    $devenv = @(Get-Process devenv -ErrorAction SilentlyContinue)
    if ($devenv.Count -ne 1) { throw "Start exactly one Visual Studio 2026 (found $($devenv.Count) running)." }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    $instances = & $vswhere -all -prerelease -products * -format json | ConvertFrom-Json
    $instance = $instances | Where-Object { $devenv[0].Path -like "$($_.installationPath)\*" } | Select-Object -First 1
    if (-not $instance) { throw "Can't find the Visual Studio installation of $($devenv[0].Path)." }
    $path = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio\18.0_$($instance.instanceId)\settings.json"
    if (-not (Test-Path $path)) { throw "Visual Studio's settings file $path doesn't exist." }
    return $path
}

# Runs the published tool with the default appsettings.jsonc changed as given; returns its output.
function Invoke-Tool([string[]] $arguments = @(), [hashtable] $settings = @{}) {
    $text = $script:defaultSettings
    foreach ($key in $settings.Keys) {
        $pattern = '"' + $key + '": [^,\r\n]*'
        if ($text -notmatch $pattern) { throw "appsettings.jsonc has no ""$key"" line to change." }
        $replacement = '"' + $key + '": ' + (ConvertTo-JsonString $settings[$key])
        $text = [regex]::Replace($text, $pattern, { $replacement }.GetNewClosure())
    }
    [IO.File]::WriteAllText((Join-Path $script:toolDir "appsettings.jsonc"), $text, (New-Object Text.UTF8Encoding $false))
    $output = & (Join-Path $script:toolDir "CSharpCodeColors.exe") @arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "CSharpCodeColors $arguments failed:`n$output" }
    return $output
}

# Switches Visual Studio to its "dark" or "light" theme and waits until the editor colors show it.
function Set-VisualStudioTheme([string] $kind) {
    $json = $script:originalVsSettings -replace '\s*"environment\.visualExperience\.colorTheme":\s*"[^"]*",?', ''
    $json = ([regex]'\{').Replace($json, "{`r`n  ""environment.visualExperience.colorTheme"": ""$kind"",", 1)
    [IO.File]::WriteAllText($script:vsSettingsPath, $json, (New-Object Text.UTF8Encoding $false))

    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $list = Invoke-Tool @("--list")
        # "Plain Text  <foreground>  <background>", where a color is "#RRGGBB" or "default -> #RRGGBB".
        if ($list -match '(?m)^Plain Text\s+(?:default -> )?#[0-9A-F]{6}\s+(?:default -> )?#([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})') {
            $luminance = (0.2126 * [Convert]::ToInt32($Matches[1], 16) + 0.7152 * [Convert]::ToInt32($Matches[2], 16) + 0.0722 * [Convert]::ToInt32($Matches[3], 16)) / 255
            if (($kind -eq "dark") -eq ($luminance -lt 0.5)) { return }
        }
        Start-Sleep -Seconds 2
    }
    throw "Visual Studio didn't switch to its $kind theme within 90 seconds."
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("csharpcodecolors-presets-" + [Guid]::NewGuid().ToString("N"))
$script:toolDir = Join-Path $work "tool"
New-Item -ItemType Directory -Force (Join-Path $OutputDirectory "adapted") | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

Write-Host "Publishing the tool..."
& dotnet publish (Join-Path $repo "src\CSharpCodeColors") -c Release -o $script:toolDir --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
$script:defaultSettings = [IO.File]::ReadAllText((Join-Path $script:toolDir "appsettings.jsonc"))

$script:vsSettingsPath = Get-VisualStudioSettingsPath
$originalBytes = [IO.File]::ReadAllBytes($script:vsSettingsPath)
$script:originalVsSettings = [IO.File]::ReadAllText($script:vsSettingsPath)
try {
    foreach ($pass in @(
        @{ Kind = "dark"; Scope = "Dark 2026"; Themes = $darkThemes },
        @{ Kind = "light"; Scope = "Light 2026"; Themes = $lightThemes })) {
        Write-Host "Switching Visual Studio to its $($pass.Kind) theme..."
        Set-VisualStudioTheme $pass.Kind

        Write-Host "  Visual Studio's own colors for ""$($pass.Scope)"""
        Invoke-Tool -settings @{ OutputPath = (Join-Path $work "visual-studio-$($pass.Kind).json"); VsCodeThemeScope = $pass.Scope } | Out-Null

        foreach ($theme in $pass.Themes) {
            $file = Join-Path $OutputDirectory "adapted\$(Get-PresetName $theme).json"
            $output = Invoke-Tool -settings @{ OutputPath = $file; AdaptToVsCodeTheme = $theme }
            $summary = [regex]::Match($output, 'Adapted to "[^"]*": [^,]*, \d+ get a new color').Value
            Write-Host "  $theme -> adapted\$(Split-Path $file -Leaf): $summary"
        }
    }
}
finally {
    [IO.File]::WriteAllBytes($script:vsSettingsPath, $originalBytes)
    Write-Host "Restored Visual Studio's settings."
}

& dotnet run (Join-Path $PSScriptRoot "merge-settings.cs") -- (Join-Path $OutputDirectory "visual-studio-2026.json") `
    (Join-Path $work "visual-studio-dark.json") (Join-Path $work "visual-studio-light.json")
if ($LASTEXITCODE -ne 0) { throw "Merging the Visual Studio presets failed." }
Write-Host "Wrote visual-studio-2026.json and $($darkThemes.Count + $lightThemes.Count) adapted presets to $OutputDirectory."
Remove-Item -Recurse -Force $work
