# CSharpCodeColors

Reads the C# colors of the running Visual Studio 2026 (Tools > Options > Fonts and Colors, including the
active theme and your customizations) and writes a VS Code `settings.json` fragment with **C#-only**
overrides: `editor.semanticTokenColorCustomizations` and `editor.tokenColorCustomizations`. Other languages
and the rest of your VS Code theme are left alone.

## Ready-made settings

If you just want the C# colors of Visual Studio 2026's Dark and Light themes, you don't need to run the tool:
[`settings.json`](settings.json) in the repo root is the tool's combined output for both themes. Each of its
two keys holds a `"[Dark 2026]"` block and a `"[Light 2026]"` block, and VS Code applies a block only while
the color theme with exactly that name is active.

To use the colors with your own themes, rename the blocks before merging (4 places: 2 in
`editor.semanticTokenColorCustomizations`, 2 in `editor.tokenColorCustomizations`):

```jsonc
"editor.semanticTokenColorCustomizations": {
  "[Default Dark Modern]": { ... },   // was "[Dark 2026]"
  "[Default Light Modern]": { ... }   // was "[Light 2026]"
},
"editor.tokenColorCustomizations": {
  "[Default Dark Modern]": { ... },   // was "[Dark 2026]"
  "[Default Light Modern]": { ... }   // was "[Light 2026]"
}
```

- Use the theme name exactly as **Preferences: Color Theme** (Ctrl+K Ctrl+T) shows it, including case and spaces.
- One block can cover several themes, as in `"[Default Dark Modern][Dark+]"`, or use a wildcard, as in
  `"[Default Dark*]"`.
- A block whose name doesn't match an installed theme does nothing.

Then merge the file into your user settings as described in [Merging into VS Code](#merging-into-vs-code).

To generate a file for your own themes, run the tool once per Visual Studio theme with `VsCodeThemeScope`
set to the matching VS Code theme name, then combine the blocks.

Running the tool from the repo root with the default `OutputPath` overwrites this file.

## Build and run

Requires the .NET 10 SDK and Visual Studio 2026 running (exactly one instance).

```powershell
dotnet run --project src\CSharpCodeColors            # writes .\settings.json
dotnet run --project src\CSharpCodeColors -- --list  # lists every Fonts and Colors item VS exposes
```

Or publish once and run the exe (keep `appsettings.json` next to it):

```powershell
dotnet publish src\CSharpCodeColors -c Release -o publish
publish\CSharpCodeColors.exe
```

Settings are in `appsettings.json` next to the executable:

| Key | Meaning |
|---|---|
| `OutputPath` | Output file, relative to the current directory. Default `settings.json`. Any writable path works, including your real VS Code user settings. |
| `VsCodeThemeScope` | `null`: the overrides apply under every VS Code theme. A theme name such as `"Default Dark Modern"`: both keys are wrapped in `"[Default Dark Modern]": { ... }`. |
| `Mappings` | VS item → VS Code selectors. `VisualStudioItem` is the name as `--list` prints it; `SemanticTokens` entries must end in `:csharp`; `TextMateScopes` must end in `.cs` or start with `source.cs`. A selector used by two mappings is an error. |

Warnings (an item VS doesn't have, a background VS Code can't show) don't fail the run; they are listed in
the summary at the end. The exit code is non-zero only for fatal errors (VS not running or more than one
instance, VS busy or hung, malformed `appsettings.json`, unwritable output path).

If VS shows a modal dialog, close it; the tool waits up to 20 seconds for VS to accept calls, and gives up
after 60 seconds if VS doesn't answer at all.

Note: `"enabled": true` in `editor.semanticTokenColorCustomizations` is theme-wide in VS Code, not per
language. With a theme that leaves semantic highlighting off, it also turns it on for JS/TS/Python. Set
`VsCodeThemeScope` to limit it to one theme, or remove the line and instead add
`"[csharp]": { "editor.semanticHighlighting.enabled": true }` to your user settings, which is per language.

## Merging into VS Code

Open your user settings JSON in VS Code (**Preferences: Open User Settings (JSON)**) and copy the two keys
from the generated `settings.json` into it, replacing any existing `editor.semanticTokenColorCustomizations`
/ `editor.tokenColorCustomizations` blocks (or merge by hand if you already have rules there you want to keep).
Rerun the tool and re-paste whenever you change colors or the theme in Visual Studio.

## Layout

- `src/CSharpCodeColors` – the tool, a .NET Generic Host app: `appsettings.json` is read through
  `Microsoft.Extensions.Configuration` (it holds the default mapping), output goes through `Microsoft.Extensions.Logging`.
- `tests/CSharpCodeColors.Tests` – TUnit unit tests (no VS needed). Run them with `dotnet test`; `global.json`
  selects the Microsoft.Testing.Platform runner that TUnit needs.
- `sample/` – a C# project exercising every classification in the default mapping.
