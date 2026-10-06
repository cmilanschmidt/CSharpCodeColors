using CSharpCodeColors.Exceptions;

namespace CSharpCodeColors.Services.VsCode;

internal static class OutputFileWriter
{
    public static void Write(string path, string content)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, content);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new FatalException($"Can't write {path}: {e.Message}");
        }
    }
}
