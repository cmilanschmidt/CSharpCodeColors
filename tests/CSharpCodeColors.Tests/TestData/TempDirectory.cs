namespace CSharpCodeColors.Tests.TestData;

/// <summary>A temporary directory with helpers to write files into it; deleted on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    /// <summary>Writes <paramref name="content"/> to a path relative to the directory and returns the full path.</summary>
    public string Write(string relativePath, string content)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
