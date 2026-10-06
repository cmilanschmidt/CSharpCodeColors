using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Services.VsCode;

namespace CSharpCodeColors.Tests.Services.VsCode;

public class OutputFileWriterTests
{
    [Test]
    public async Task Writes_and_creates_missing_directories()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "sub");
        string file = Path.Combine(dir, "out.json");
        try
        {
            OutputFileWriter.Write(file, "{}");
            await Assert.That(File.ReadAllText(file)).IsEqualTo("{}");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Test]
    public async Task Unwritable_path_is_fatal()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // A directory with the output file's name can't be overwritten.
            Directory.CreateDirectory(Path.Combine(dir, "out.json"));
            await Assert.That(() => OutputFileWriter.Write(Path.Combine(dir, "out.json"), "{}")).Throws<FatalException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Invalid_path_is_fatal() =>
        await Assert.That(() => OutputFileWriter.Write("bad\0name.json", "{}")).Throws<FatalException>();
}
