using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.Colors;
using CSharpCodeColors.Services.Colors;
using static CSharpCodeColors.Tests.TestData.TestItems;

namespace CSharpCodeColors.Tests.Services.Colors;

public class ColorResolverTests
{
    [Test]
    public async Task Explicit_color_is_used_as_is()
    {
        var colors = Resolver(Explicit("class name", 0x4EC9B0));
        await Assert.That(colors.Foreground("class name")).IsEqualTo(new EffectiveColor(Rgb(0x4EC9B0), "class name"));
    }

    [Test]
    public async Task Default_color_inherits_from_base_classification()
    {
        // Seen in VS: with "class name" red, an untouched "record class name" renders red.
        var colors = Resolver(Explicit("class name", 0xFF0000), Default("record class name"), Default("Identifier"));
        await Assert.That(colors.Foreground("record class name")).IsEqualTo(new EffectiveColor(Rgb(0xFF0000), "class name"));
    }

    [Test]
    public async Task Default_chain_ends_at_plain_text()
    {
        var colors = Resolver(Default("property name"), Default("Identifier"));
        await Assert.That(colors.Foreground("property name")).IsEqualTo(new EffectiveColor(PlainForeground, "Plain Text"));
    }

    [Test]
    public async Task Explicit_identifier_is_inherited_by_identifier_kinds()
    {
        var colors = Resolver(Default("field name"), Explicit("Identifier", 0x112233));
        await Assert.That(colors.Foreground("field name").Color).IsEqualTo(Rgb(0x112233));
    }

    [Test]
    public async Task Missing_base_item_is_skipped()
    {
        // "Identifier" is not present: "field name" falls through to Plain Text.
        var colors = Resolver(Default("field name"));
        await Assert.That(colors.Foreground("field name").Color).IsEqualTo(PlainForeground);
    }

    [Test]
    public async Task Json_property_name_inherits_from_method_name()
    {
        var colors = Resolver(Default("json - property name"), Explicit("method name", 0xDCDCAA));
        await Assert.That(colors.Foreground("json - property name").Color).IsEqualTo(Rgb(0xDCDCAA));
    }

    [Test]
    public async Task Plain_text_default_uses_the_color_vs_resolved()
    {
        var plain = new VsColorItem("Plain Text", VsColor.Default(Rgb(0x000000)), VsColor.Default(Rgb(0xFFFFFF)), false);
        var colors = new ColorResolver([plain]);
        await Assert.That(colors.Foreground("Plain Text").Color).IsEqualTo(Rgb(0x000000));
        await Assert.That(colors.Background("Plain Text").Color).IsEqualTo(Rgb(0xFFFFFF));
    }

    [Test]
    public async Task Names_are_case_insensitive()
    {
        var colors = Resolver(Explicit("Keyword", 0x569CD6), Default("keyword - control"));
        await Assert.That(colors.Foreground("KEYWORD - CONTROL").Color).IsEqualTo(Rgb(0x569CD6));
    }

    [Test]
    public async Task Missing_plain_text_is_fatal() =>
        await Assert.That(() => new ColorResolver([Explicit("Keyword", 0x569CD6)])).Throws<FatalException>();

    [Test]
    [Arguments("record class name")]
    [Arguments("extension method name")]
    [Arguments("json - property name")]
    [Arguments("json - constructor name")]
    [Arguments("Number")]
    [Arguments("unknown item")]
    public async Task Every_base_chain_ends_at_plain_text(string name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (string? current = name; current != null; current = ClassificationHierarchy.BaseOf(current))
            await Assert.That(seen.Add(current)).IsTrue().Because($"cycle at {current}");
        await Assert.That(seen).Contains("Plain Text");
    }
}
