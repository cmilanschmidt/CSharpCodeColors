using CSharpCodeColors.Constants;
using CSharpCodeColors.Tests.TestData;

namespace CSharpCodeColors.Tests.Options;

public class DefaultMappingTests
{
    [Test]
    public async Task Default_mapping_is_valid()
    {
        // The tool's appsettings.jsonc is copied to the test output through the project reference.
        var settings = TestOptions.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, FileNames.AppSettings)));
        await Assert.That(settings.Mappings).IsNotEmpty();
    }
}
