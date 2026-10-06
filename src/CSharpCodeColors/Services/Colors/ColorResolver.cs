using CSharpCodeColors.Constants;
using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Services.Colors;

/// <summary>
/// Resolves Default colors the way the Visual Studio editor does: an item without an explicit color
/// takes the color of its base classification, ending at Plain Text.
/// </summary>
internal sealed class ColorResolver
{
    private readonly Dictionary<string, VsColorItem> _items;

    public ColorResolver(IEnumerable<VsColorItem> items)
    {
        _items = new Dictionary<string, VsColorItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            _items.TryAdd(item.Name, item);
        if (!_items.ContainsKey(ClassificationNames.PlainText))
            throw new FatalException($"Visual Studio did not report a '{ClassificationNames.PlainText}' item, so colors can't be resolved.");
    }

    public VsColorItem PlainText => _items[ClassificationNames.PlainText];

    public VsColorItem? Find(string name) => _items.GetValueOrDefault(name);

    public EffectiveColor Foreground(string name) => Resolve(name, item => item.Foreground);

    public EffectiveColor Background(string name) => Resolve(name, item => item.Background);

    private EffectiveColor Resolve(string name, Func<VsColorItem, VsColor> select)
    {
        for (string? current = name; current != null; current = ClassificationHierarchy.BaseOf(current))
        {
            if (!_items.TryGetValue(current, out var item))
                continue;
            var color = select(item);
            if (!color.IsDefault || current.Equals(ClassificationNames.PlainText, StringComparison.OrdinalIgnoreCase))
                return new EffectiveColor(color.Color, item.Name);
        }
        throw new InvalidOperationException("Unreachable: every chain ends at Plain Text.");
    }
}
