namespace CSharpCodeColors.Options;

/// <summary>The appsettings.json the settings come from, and why loading it failed, if it did.</summary>
internal sealed class AppSettingsFile(string path)
{
    public string Path { get; } = path;

    /// <summary>Set when the file is missing, unreadable or not valid JSON; reported when the settings are read.</summary>
    public string? LoadError { get; set; }
}
