# CSharpCodeColors

Reads the C# colors of the running Visual Studio 2026 (Tools > Options > Fonts and Colors, including the
active theme and your customizations) and writes a VS Code `settings.json` fragment with **C#-only**
overrides: `editor.semanticTokenColorCustomizations` and `editor.tokenColorCustomizations`. Other languages
and the rest of your VS Code theme are left alone.

It writes the colors in one of two ways:

- **Visual Studio's colors** (the default): C# looks exactly as in Visual Studio, whatever the VS Code theme.
- **Adapted to a VS Code theme** (`AdaptToVsCodeTheme`): C# keeps the theme's own colors, and gets new colors
  that fit the theme only where Visual Studio tells apart what the theme doesn't (structs from classes,
  properties from locals, and so on). See [Adapting to a VS Code theme](#adapting-to-a-vs-code-theme).

## Ready-made settings

You don't need to run the tool for the C# colors of Visual Studio 2026's Dark and Light themes; the
[`presets`](presets) folder has the tool's output:

| File | What it holds |
|---|---|
| [`presets/visual-studio-2026.json`](presets/visual-studio-2026.json) | Visual Studio's own colors, in a `"[Dark 2026]"` block (from VS Dark) and a `"[Light 2026]"` block (from VS Light). |
| [`presets/adapted/*.json`](presets/adapted) | Visual Studio's colors adapted to one built-in VS Code theme each: VS Dark adapted to `dark-modern.json`, `dark-plus.json`, `dark-2026.json`, `visual-studio-dark.json`, `monokai.json`, `monokai-dimmed.json`, `solarized-dark.json`, `abyss.json`, `kimbie-dark.json`, `red.json` and `tomorrow-night-blue.json`; VS Light adapted to `light-modern.json`, `light-plus.json`, `light-2026.json`, `visual-studio-light.json`, `solarized-light.json` and `quiet-light.json`. |

[`presets/README.md`](presets/README.md) shows each preset next to its theme as it is, such as Dark Modern:

![Dark Modern as it is (left) and with the adapted Visual Studio colors (right)](presets/previews/dark-modern.png)

Each file's two keys hold one block per theme, such as `"[Dark Modern]": { ... }`, and VS Code applies a block
only while the color theme with exactly that name is active. So the adapted files work as they are, and you
can combine several (copy each file's blocks into the same two keys) to have C# colors that fit whichever of
them you switch to.

To use Visual Studio's own colors with other themes, rename the blocks of `visual-studio-2026.json` before
merging (4 places: 2 in `editor.semanticTokenColorCustomizations`, 2 in `editor.tokenColorCustomizations`):

```jsonc
"editor.semanticTokenColorCustomizations": {
  "[Dark Modern]": { ... },    // was "[Dark 2026]"
  "[Light Modern]": { ... }    // was "[Light 2026]"
},
"editor.tokenColorCustomizations": {
  "[Dark Modern]": { ... },    // was "[Dark 2026]"
  "[Light Modern]": { ... }    // was "[Light 2026]"
}
```

- Use the theme's name exactly as `CSharpCodeColors --themes` lists it in the first column, including case
  and spaces. That is usually the name in **Preferences: Color Theme** (Ctrl+K Ctrl+T), but not always: the
  theme picker's "Dark (Visual Studio)" is `"[Visual Studio Dark]"`. Older VS Code versions named the
  default themes "Default Dark Modern" and so on; current ones use "Dark Modern".
- One block can cover several themes, as in `"[Dark Modern][Dark+]"`, or use a wildcard, as in `"[Dark*]"`.
- A block whose name doesn't match an installed theme does nothing.

Then merge the file into your user settings as described in [Merging into VS Code](#merging-into-vs-code).

To generate a file for your own themes, run the tool once per Visual Studio theme with `AdaptToVsCodeTheme`
or `VsCodeThemeScope` set to the VS Code theme name, then combine the blocks.

## Build and run

Requires the .NET 10 SDK and Visual Studio 2026 running (exactly one instance).

```powershell
dotnet run --project src\CSharpCodeColors              # writes .\settings.json
dotnet run --project src\CSharpCodeColors -- --list    # lists every Fonts and Colors item VS exposes
dotnet run --project src\CSharpCodeColors -- --themes  # lists the installed VS Code color themes
```

Or publish once and run the exe (keep `appsettings.jsonc` next to it):

```powershell
dotnet publish src\CSharpCodeColors -c Release -o publish
publish\CSharpCodeColors.exe
```

Settings are in `appsettings.jsonc` next to the executable:

| Key | Meaning |
|---|---|
| `OutputPath` | Output file, relative to the current directory. Default `settings.json`. Any writable path works, including your real VS Code user settings. |
| `VsCodeThemeScope` | `null`: the overrides apply under every VS Code theme. A theme name such as `"Dark Modern"`: both keys are wrapped in `"[Dark Modern]": { ... }`. Must be `null` with `AdaptToVsCodeTheme`, whose output is always wrapped in the adapted theme's name. |
| `AdaptToVsCodeTheme` | `null`: write Visual Studio's colors. A VS Code theme name as `--themes` lists it, such as `"Dark Modern"`: adapt the colors to that theme (see below). |
| `VsCodePath` | The VS Code folder that contains `Code.exe`, for `AdaptToVsCodeTheme` and `--themes`. `null`: the default install locations and the `code` command on `PATH`. |
| `Mappings` | VS item → VS Code selectors. `VisualStudioItem` is the name as `--list` prints it; `SemanticTokens` entries must end in `:csharp`; `TextMateScopes` must end in `.cs` or start with `source.cs`. A selector used by two mappings is an error. |

Warnings (an item VS doesn't have, a background VS Code can't show) don't fail the run; they are listed in
the summary at the end. The exit code is non-zero only for fatal errors (VS not running or more than one
instance, VS busy or hung, malformed `appsettings.jsonc`, unwritable output path, VS Code or the theme to
adapt to not found).

If VS shows a modal dialog, close it; the tool waits up to 20 seconds for VS to accept calls, and gives up
after 60 seconds if VS doesn't answer at all.

Note: `"enabled": true` in `editor.semanticTokenColorCustomizations` is theme-wide in VS Code, not per
language. With a theme that leaves semantic highlighting off, it also turns it on for JS/TS/Python. Set
`VsCodeThemeScope` to limit it to one theme, or remove the line and instead add
`"[csharp]": { "editor.semanticHighlighting.enabled": true }` to your user settings, which is per language.

## Adapting to a VS Code theme

With `AdaptToVsCodeTheme` set, the tool reads that theme from your VS Code installation (built-in or from an
extension, following its `include` chain) and works out the color it gives each mapped C# token, the way
VS Code does: the theme's semantic token rules, else the TextMate scopes VS Code and the C# extension
register for the token, else the editor foreground. Then, for each mapped item:

- **The theme has its own color for it** (keywords, strings, comments, methods in Dark Modern): the theme's
  color is kept, even where Visual Studio's differs slightly.
- **The theme shows it like items Visual Studio colors differently** (Dark Modern shows structs, interfaces
  and type parameters in the class color; properties and fields in the local variable color): the most
  important of those items (class, local) keeps the theme's color, and the others get a new color. A new color
  is the theme's color changed the way Visual Studio's colors differ: in Dark Modern, structs get the green
  Visual Studio uses, and properties a grayer shade of the locals' blue. Small hue differences turn the
  theme's hue by the same angle; large ones take Visual Studio's hue in the theme's lightness and saturation.
- **The theme tells apart what Visual Studio doesn't** (Dark Modern colors enum members and constants,
  Visual Studio leaves them plain): the theme's colors are kept.

New colors snap to a nearby color of the theme's palette when one is free, stay within the theme's lightness
and saturation range, are at least as readable against the editor background as the theme's own colors, and
stay distinguishable from every item that either Visual Studio or the theme shows differently. Font styles
are left to the theme (Monokai keeps its italics), except that a bold "static symbol" stays bold.

The summary lists every item with its Visual Studio color, the theme's color and the color written, and how
the color was chosen.

Adapt from a Visual Studio theme of the same kind: VS Dark for dark VS Code themes, VS Light for light ones
(the tool warns otherwise). High contrast themes are better used as they are.

## Regenerating the presets

Two scripts (Windows PowerShell 5.1 or later) rebuild the `presets` folder, for example after changing the
mapping or the adaptation, or for a newer Visual Studio or VS Code:

```powershell
scripts\generate-presets.ps1    # presets\visual-studio-2026.json and presets\adapted\*.json
scripts\generate-previews.ps1   # presets\previews\*.png (-Preset dark-modern,monokai for some only)
```

- `generate-presets.ps1` needs the .NET 10 SDK, VS Code and one running Visual Studio 2026. It switches Visual
  Studio to its Dark and then its Light theme (through Visual Studio's settings.json, which it puts back as it
  was), and runs the tool for each theme. `merge-settings.cs` combines the Dark and Light blocks.
- `generate-previews.ps1` needs VS Code with the C# extension and 100% display scaling. It runs a separate VS
  Code instance with a temporary profile on a copy of `sample\`, captures `Sample.cs` in each theme without and
  with its preset, and closes only that instance. Your VS Code windows and settings aren't touched.

## Merging into VS Code

Open your user settings JSON in VS Code (**Preferences: Open User Settings (JSON)**) and copy the two keys
from the generated `settings.json` into it, replacing any existing `editor.semanticTokenColorCustomizations`
/ `editor.tokenColorCustomizations` blocks (or merge by hand if you already have rules there you want to keep).
Rerun the tool and re-paste whenever you change colors or the theme in Visual Studio.

## Layout

- `src/CSharpCodeColors` – the tool, a .NET Generic Host app: `appsettings.jsonc` is read through
  `Microsoft.Extensions.Configuration` (it holds the default mapping), output goes through `Microsoft.Extensions.Logging`.
- `tests/CSharpCodeColors.Tests` – TUnit unit tests (no VS needed). Run them with `dotnet test`; `global.json`
  selects the Microsoft.Testing.Platform runner that TUnit needs.
- `sample/` – a C# project exercising every classification in the default mapping.
- `presets/` – the tool's output for Visual Studio 2026's Dark and Light themes, as is and adapted to the
  built-in VS Code themes, with preview images.
- `scripts/` – the scripts that regenerate `presets/`.

## AI disclosure

This project was implemented with the help of Claude Code, Anthropic's AI coding assistant, which wrote much of
the code, tests, scripts and documentation under human direction and review. The presets and preview images
were generated by the tool and scripts in this repository.
