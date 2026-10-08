using System.Text.RegularExpressions;

namespace CSharpCodeColors.Services.VsCode.Themes;

/// <summary>
/// A TextMate scope selector of a theme rule ("keyword - keyword.operator", "source.cs entity.name.type, support.type"),
/// matched the way VS Code matches it when it looks up a theme color for a list of scopes: the same parser and the
/// same score as getScopeMatcher/nameMatcher in VS Code's colorThemeData.ts.
/// </summary>
internal sealed class ScopeSelector
{
    private static readonly Regex Tokenizer = new(@"([LR]:|[\w\.:][\w\.:\-]*|[\,\|\-\(\)])", RegexOptions.CultureInvariant);

    private readonly List<Func<IReadOnlyList<string>, int>> _alternatives = [];

    private ScopeSelector() { }

    /// <summary>Parses a rule's "scope": a string, or the strings of an array (any of them may match).</summary>
    public static ScopeSelector Parse(IEnumerable<string> selectors)
    {
        var result = new ScopeSelector();
        foreach (var selector in selectors)
            new Parser(selector, result._alternatives).ParseSelector();
        return result;
    }

    public static ScopeSelector Parse(string selector) => Parse([selector]);

    public bool IsEmpty => _alternatives.Count == 0;

    /// <summary>
    /// The score of the best matching alternative for <paramref name="scopes"/> (outermost first), or -1.
    /// A higher score is a more specific match: deeper in the scope list, then a longer selector name.
    /// </summary>
    public int Match(IReadOnlyList<string> scopes)
    {
        int best = -1;
        foreach (var alternative in _alternatives)
            best = Math.Max(best, alternative(scopes));
        return best;
    }

    /// <summary>VS Code's nameMatcher: every identifier must match some scope; the score comes from the last one.</summary>
    private static int MatchNames(IReadOnlyList<string> identifiers, IReadOnlyList<string> scopes)
    {
        if (scopes.Count < identifiers.Count)
            return -1;
        int score = -1;
        foreach (var identifier in identifiers)
        {
            bool found = false;
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (ScopeMatches(scopes[i], identifier))
                {
                    score = (i + 1) * 0x10000 + identifier.Length;
                    found = true;
                    break;
                }
            }
            if (!found)
                return -1;
        }
        return score;
    }

    /// <summary>True if <paramref name="scope"/> is <paramref name="prefix"/> or starts with it followed by a dot.</summary>
    public static bool ScopeMatches(string scope, string prefix) =>
        scope.Length == prefix.Length ? scope == prefix
        : scope.Length > prefix.Length && scope[prefix.Length] == '.' && scope.StartsWith(prefix, StringComparison.Ordinal);

    /// <summary>A port of createMatchers from vscode-textmate, as VS Code uses it for theme rules.</summary>
    private sealed class Parser(string selector, List<Func<IReadOnlyList<string>, int>> results)
    {
        private readonly IEnumerator<string> _tokens = Tokenizer.Matches(selector).Select(m => m.Value).GetEnumerator();
        private string? _token;

        private void Next() => _token = _tokens.MoveNext() ? _tokens.Current : null;

        public void ParseSelector()
        {
            Next();
            while (_token != null)
            {
                if (_token.Length == 2 && _token[1] == ':')
                    Next(); // L:/R: priority prefixes don't change the score.
                var conjunction = ParseConjunction();
                if (conjunction != null)
                    results.Add(conjunction);
                if (_token != ",")
                    break;
                Next();
            }
        }

        private Func<IReadOnlyList<string>, int>? ParseOperand()
        {
            if (_token == "-")
            {
                Next();
                var negated = ParseOperand();
                return negated == null ? null : scopes => negated(scopes) < 0 ? 0 : -1;
            }
            if (_token == "(")
            {
                Next();
                var inner = ParseInnerExpression();
                if (_token == ")")
                    Next();
                return inner;
            }
            if (IsIdentifier(_token))
            {
                var identifiers = new List<string>();
                do
                {
                    identifiers.Add(_token!);
                    Next();
                } while (IsIdentifier(_token));
                return scopes => MatchNames(identifiers, scopes);
            }
            return null;
        }

        private Func<IReadOnlyList<string>, int>? ParseConjunction()
        {
            var operands = new List<Func<IReadOnlyList<string>, int>>();
            for (var operand = ParseOperand(); operand != null; operand = ParseOperand())
                operands.Add(operand);
            if (operands.Count == 0)
                return null;
            return scopes =>
            {
                int score = operands[0](scopes);
                for (int i = 1; score >= 0 && i < operands.Count; i++)
                    score = Math.Min(score, operands[i](scopes));
                return score;
            };
        }

        private Func<IReadOnlyList<string>, int>? ParseInnerExpression()
        {
            var alternatives = new List<Func<IReadOnlyList<string>, int>>();
            for (var conjunction = ParseConjunction(); conjunction != null; conjunction = ParseConjunction())
            {
                alternatives.Add(conjunction);
                if (_token is not ("|" or ","))
                    break;
                do
                    Next();
                while (_token is "|" or ",");
            }
            if (alternatives.Count == 0)
                return null;
            return scopes => alternatives.Max(a => a(scopes));
        }

        private static bool IsIdentifier(string? token) => token != null && Regex.IsMatch(token, @"[\w\.:]+");
    }
}
