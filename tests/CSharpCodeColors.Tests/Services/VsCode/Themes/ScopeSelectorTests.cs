using CSharpCodeColors.Services.VsCode.Themes;

namespace CSharpCodeColors.Tests.Services.VsCode.Themes;

public class ScopeSelectorTests
{
    private static int Match(string selector, params string[] scopes) => ScopeSelector.Parse(selector).Match(scopes);

    [Test]
    public async Task Selector_matches_its_scope_and_the_scopes_under_it()
    {
        await Assert.That(Match("keyword", "keyword")).IsGreaterThanOrEqualTo(0);
        await Assert.That(Match("keyword", "keyword.control.cs")).IsGreaterThanOrEqualTo(0);
        await Assert.That(Match("keyword", "keywords")).IsEqualTo(-1);
        await Assert.That(Match("keyword.control", "keyword")).IsEqualTo(-1);
    }

    [Test]
    public async Task Longer_selector_scores_higher()
    {
        // Why controlKeyword ("keyword.control.cs") gets Dark+'s purple and keyword ("keyword.cs") the blue.
        await Assert.That(Match("keyword.control", "keyword.control.cs")).IsGreaterThan(Match("keyword", "keyword.control.cs"));
    }

    [Test]
    public async Task Match_on_a_deeper_scope_scores_higher()
    {
        await Assert.That(Match("variable", "source.cs", "variable.other.object.cs")).IsGreaterThan(Match("source.cs", "source.cs", "variable.other.object.cs"));
    }

    [Test]
    public async Task Descendant_selector_needs_every_part()
    {
        await Assert.That(Match("source.cs comment", "source.cs", "comment.block.cs")).IsGreaterThanOrEqualTo(0);
        await Assert.That(Match("source.js comment", "source.cs", "comment.block.cs")).IsEqualTo(-1);
        await Assert.That(Match("source.cs comment", "comment.block.cs")).IsEqualTo(-1);
    }

    [Test]
    public async Task Exclusion_rejects_the_excluded_scope()
    {
        // From VS Code's own "keywords" token color customization.
        await Assert.That(Match("keyword - keyword.operator", "keyword.control.cs")).IsGreaterThanOrEqualTo(0);
        await Assert.That(Match("keyword - keyword.operator", "keyword.operator.cs")).IsEqualTo(-1);
    }

    [Test]
    public async Task Comma_and_array_entries_are_alternatives()
    {
        await Assert.That(Match("string, comment", "comment.line.cs")).IsGreaterThanOrEqualTo(0);
        await Assert.That(ScopeSelector.Parse(["string", "comment"]).Match(["comment.line.cs"])).IsGreaterThanOrEqualTo(0);
        await Assert.That(Match("string, comment", "keyword.cs")).IsEqualTo(-1);
    }

    [Test]
    public async Task Empty_selector_matches_nothing()
    {
        await Assert.That(ScopeSelector.Parse("").IsEmpty).IsTrue();
        await Assert.That(Match("", "source.cs")).IsEqualTo(-1);
    }
}
