using System.Text.Json;
using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.VsCode.Themes;

namespace CSharpCodeColors.Services.VsCode.Themes;

/// <summary>
/// The installed VS Code extensions, built-in and user-installed, read from their package.json files: the color
/// themes they contribute and their semantic token types and scopes.
/// </summary>
internal sealed class VsCodeExtensions
{
    private static readonly JsonDocumentOptions JsonOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public required string InstallPath { get; init; }

    public required IReadOnlyList<ThemeContribution> Themes { get; init; }

    public required SemanticTokenRegistry SemanticTokens { get; init; }

    /// <summary>
    /// Finds VS Code in <paramref name="installPath"/> (the folder with Code.exe), or in its default locations when
    /// that is null, and reads its built-in extensions and the user's extensions.
    /// </summary>
    public static VsCodeExtensions Load(string? installPath)
    {
        var (install, builtIn) = FindInstallation(installPath);
        var userExtensions = UserExtensionDirectories(install);

        var themes = new List<ThemeContribution>();
        var registry = new SemanticTokenRegistry();
        foreach (var (directory, isBuiltIn) in builtIn.Select(d => (d, true)).Concat(userExtensions.Select(d => (d, false))))
        {
            try
            {
                ReadExtension(directory, isBuiltIn, themes, registry);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                // A broken extension doesn't stop VS Code either.
            }
        }
        return new VsCodeExtensions { InstallPath = install, Themes = themes, SemanticTokens = registry };
    }

    /// <summary>
    /// The theme whose settings id ("[Name]" in settings) or label (the name in the theme picker) is
    /// <paramref name="name"/>: an exact match first, then ignoring case.
    /// </summary>
    public ThemeContribution FindTheme(string name)
    {
        var match = Themes.FirstOrDefault(t => t.SettingsId == name)
            ?? Themes.FirstOrDefault(t => t.Label == name)
            ?? Themes.FirstOrDefault(t => t.SettingsId.Equals(name, StringComparison.OrdinalIgnoreCase) || t.Label.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;
        var names = Themes.Select(t => t.SettingsId).Distinct().Order(StringComparer.OrdinalIgnoreCase);
        throw new FatalException($"VS Code ({InstallPath}) has no color theme named \"{name}\". Installed themes: {string.Join(", ", names.Select(n => $"\"{n}\""))}. Run with --themes for details.");
    }

    private static (string Install, IReadOnlyList<string> BuiltIn) FindInstallation(string? installPath)
    {
        var candidates = installPath != null
            ? [installPath]
            : new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code"),
            }.Concat(CodeOnPath()).ToArray();

        foreach (var candidate in candidates)
        {
            if (BuiltInExtensionsDirectory(candidate) is { } builtIn)
                return (candidate, Directory.GetDirectories(builtIn).Where(d => File.Exists(Path.Combine(d, "package.json"))).ToList());
        }
        string where = installPath != null ? $"in VsCodePath \"{installPath}\"" : $"in {string.Join(", ", candidates.Select(c => $"\"{c}\""))}";
        throw new FatalException($"Can't find VS Code {where}. Set VsCodePath in appsettings.jsonc to the folder that contains Code.exe.");
    }

    /// <summary>The folder of a "code" command on PATH is the bin folder inside the installation.</summary>
    private static IEnumerable<string> CodeOnPath() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => File.Exists(Path.Combine(p, "code.cmd")) || File.Exists(Path.Combine(p, "code")))
            .Select(p => Path.GetDirectoryName(Path.GetFullPath(p)))
            .OfType<string>();

    /// <summary>
    /// resources\app\extensions, either directly in the installation or, in newer versions, under a folder named
    /// after the build (the newest one if an update left more than one).
    /// </summary>
    private static string? BuiltInExtensionsDirectory(string install)
    {
        if (!Directory.Exists(install))
            return null;
        string direct = Path.Combine(install, "resources", "app", "extensions");
        if (Directory.Exists(direct))
            return direct;
        return Directory.GetDirectories(install)
            .Select(d => Path.Combine(d, "resources", "app", "extensions"))
            .Where(Directory.Exists)
            .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d))
            .FirstOrDefault();
    }

    /// <summary>
    /// The user's extensions as VS Code tracks them in extensions.json (so old versions left on disk don't count),
    /// in VSCODE_EXTENSIONS, data\extensions of a portable installation, or %USERPROFILE%\.vscode\extensions.
    /// </summary>
    private static IReadOnlyList<string> UserExtensionDirectories(string install)
    {
        string? root = Environment.GetEnvironmentVariable("VSCODE_EXTENSIONS");
        if (string.IsNullOrEmpty(root))
        {
            string portable = Path.Combine(install, "data");
            root = Directory.Exists(portable)
                ? Path.Combine(portable, "extensions")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode", "extensions");
        }
        if (!Directory.Exists(root))
            return [];

        string list = Path.Combine(root, "extensions.json");
        if (File.Exists(list))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(list), JsonOptions);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return document.RootElement.EnumerateArray()
                        .Select(e => e.TryGetProperty("relativeLocation", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null)
                        .OfType<string>()
                        .Select(r => Path.Combine(root, r))
                        .Where(Directory.Exists)
                        .ToList();
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                // Fall back to every folder.
            }
        }
        return Directory.GetDirectories(root).Where(d => File.Exists(Path.Combine(d, "package.json"))).ToList();
    }

    private static void ReadExtension(string directory, bool isBuiltIn, List<ThemeContribution> themes, SemanticTokenRegistry registry)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "package.json")), JsonOptions);
        var manifest = document.RootElement;
        if (!manifest.TryGetProperty("contributes", out var contributes) || contributes.ValueKind != JsonValueKind.Object)
            return;
        string publisher = isBuiltIn ? "vscode" : String(manifest, "publisher") ?? "";
        string extension = $"{publisher}.{String(manifest, "name")}";

        if (contributes.TryGetProperty("themes", out var themeList) && themeList.ValueKind == JsonValueKind.Array)
        {
            var nls = ReadNls(directory);
            foreach (var theme in themeList.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.Object))
            {
                string? path = String(theme, "path");
                string? label = Localize(String(theme, "label"), nls);
                string? id = String(theme, "id");
                if (path == null || (label ?? id) == null)
                    continue;
                themes.Add(new ThemeContribution(id, label ?? id!, String(theme, "uiTheme") ?? "vs-dark", Path.GetFullPath(Path.Combine(directory, path)), extension));
            }
        }
        if (contributes.TryGetProperty("semanticTokenTypes", out var types) && types.ValueKind == JsonValueKind.Array)
        {
            foreach (var type in types.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.Object))
                if (String(type, "id") is { } id)
                    registry.AddType(id, String(type, "superType"));
        }
        if (contributes.TryGetProperty("semanticTokenScopes", out var scopeList) && scopeList.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in scopeList.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
            {
                if (!entry.TryGetProperty("scopes", out var scopes) || scopes.ValueKind != JsonValueKind.Object)
                    continue;
                string? language = String(entry, "language");
                foreach (var scope in scopes.EnumerateObject().Where(s => s.Value.ValueKind == JsonValueKind.Array))
                    registry.AddScopes(scope.Name, language, scope.Value.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.String).Select(s => s.GetString()!));
            }
        }
    }

    /// <summary>package.nls.json: the English strings for %placeholders% in package.json.</summary>
    private static Dictionary<string, string> ReadNls(string directory)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string path = Path.Combine(directory, "package.nls.json");
        if (!File.Exists(path))
            return result;
        using var document = JsonDocument.Parse(File.ReadAllText(path), JsonOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.String)
                result[entry.Name] = entry.Value.GetString()!;
            else if (entry.Value.ValueKind == JsonValueKind.Object && String(entry.Value, "message") is { } message)
                result[entry.Name] = message;
        }
        return result;
    }

    private static string? Localize(string? value, Dictionary<string, string> nls) =>
        value is ['%', .. var key, '%'] && nls.TryGetValue(key, out var text) ? text : value;

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
