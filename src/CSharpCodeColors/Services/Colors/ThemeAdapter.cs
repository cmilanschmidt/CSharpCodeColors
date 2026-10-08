using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Services.Colors;

/// <summary>
/// One item to adapt: the color Visual Studio shows and the color the VS Code theme shows. Among items the theme
/// shows alike, the one with the lowest <see cref="Priority"/> keeps the theme's color; null if the item has no
/// say, in which case the item whose Visual Studio color is closest to the theme's keeps it.
/// </summary>
internal sealed record AdaptInput(string Item, RgbColor VisualStudio, RgbColor Theme, int? Priority = null);

internal enum AdaptOrigin
{
    /// <summary>The theme's own color.</summary>
    Theme,

    /// <summary>A color from elsewhere in the theme's palette, close to the derived color.</summary>
    Palette,

    /// <summary>The theme color moved the way Visual Studio's color differs from the one the theme kept.</summary>
    Derived,

    /// <summary>Derived, then moved further to stay distinguishable and readable, as far as a nearby color allows.</summary>
    Adjusted,
}

/// <summary>The adapted color of an item. <see cref="Basis"/> is the item whose theme color a new color was derived from.</summary>
internal sealed record AdaptedColor(string Item, RgbColor VisualStudio, RgbColor Theme, RgbColor Output, AdaptOrigin Origin, string? Basis);

/// <summary>
/// Gives each item a color from the VS Code theme while keeping every distinction Visual Studio makes:
/// <list type="bullet">
/// <item>Items keep the theme's color unless the theme shows them the same as items Visual Studio colors differently.</item>
/// <item>In such a group, the most important items (keywords rather than regex anchors) keep the theme color.
/// The others get a new color: the theme color changed the way their Visual Studio color differs from the kept items'.</item>
/// <item>A new color snaps to a nearby color of the theme's palette, stays inside the theme's lightness and chroma
/// range, is at least as readable as the theme's own colors, and stays distinguishable from every item that either
/// Visual Studio or the theme shows differently.</item>
/// </list>
/// </summary>
internal static class ThemeAdapter
{
    /// <summary>Visual Studio colors closer than this count as the same color.</summary>
    internal const double SameColor = 0.012;

    /// <summary>The OKLab distance kept between items that Visual Studio shows differently: clearly visible.</summary>
    internal const double VisualStudioDifference = 0.05;

    /// <summary>The OKLab distance kept between items that only the theme shows differently: still visible.</summary>
    internal const double ThemeDifference = 0.03;

    /// <summary>A new color within this distance of a palette color becomes that palette color.</summary>
    internal const double PaletteSnap = 0.07;

    /// <summary>Visual Studio colors within this much of the closest one count as equally close to the theme color.</summary>
    internal const double AnchorTolerance = 0.05;

    private sealed class ColorClass(RgbColor theme, RgbColor visualStudio)
    {
        public RgbColor Theme { get; } = theme;
        public OkLab ThemeLab { get; } = OkLab.FromRgb(theme);
        public OkLab VisualStudioLab { get; } = OkLab.FromRgb(visualStudio);
        public List<string> Items { get; } = [];
        public int? Priority { get; set; }

        /// <summary>The most important item, for the report.</summary>
        public string Name { get; set; } = "";
        public RgbColor? Output { get; set; }
        public AdaptOrigin Origin { get; set; }
        public ColorClass? Basis { get; set; }
    }

    public static IReadOnlyList<AdaptedColor> Adapt(IReadOnlyList<AdaptInput> inputs, RgbColor background, IReadOnlyList<RgbColor> palette)
    {
        // Items the theme and Visual Studio both show alike end up in one class and get one color.
        var classes = new List<ColorClass>();
        var classOf = new Dictionary<string, ColorClass>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in inputs)
        {
            var lab = OkLab.FromRgb(input.VisualStudio);
            var match = classes.FirstOrDefault(c => c.Theme == input.Theme && c.VisualStudioLab.DistanceTo(lab) < SameColor)
                ?? Add(new ColorClass(input.Theme, input.VisualStudio));
            match.Items.Add(input.Item);
            if (match.Items.Count == 1)
                match.Name = input.Item;
            if (input.Priority is { } priority && (match.Priority == null || priority < match.Priority))
                (match.Priority, match.Name) = (priority, input.Item);
            classOf[input.Item] = match;
        }

        // In each group the theme shows alike, the class with the most important item keeps the theme color. Without
        // one, the class whose Visual Studio color is closest to it; among classes about as close, the first.
        var derived = new List<ColorClass>();
        foreach (var group in classes.GroupBy(c => c.Theme))
        {
            double closest = group.Min(c => c.VisualStudioLab.DistanceTo(c.ThemeLab));
            var anchor = group.Where(c => c.Priority != null).MinBy(c => c.Priority)
                ?? group.First(c => c.VisualStudioLab.DistanceTo(c.ThemeLab) <= closest + AnchorTolerance);
            foreach (var c in group)
            {
                if (c == anchor)
                {
                    c.Output = c.Theme;
                    c.Origin = AdaptOrigin.Theme;
                }
                else
                {
                    c.Basis = anchor;
                    derived.Add(c);
                }
            }
        }

        if (derived.Count > 0)
            PlaceDerived(classes, derived, background, palette);

        return inputs.Select(i =>
        {
            var c = classOf[i.Item];
            return new AdaptedColor(i.Item, i.VisualStudio, i.Theme, c.Output!.Value, c.Origin, c.Basis?.Name);
        }).ToList();

        ColorClass Add(ColorClass c)
        {
            classes.Add(c);
            return c;
        }
    }

    private static void PlaceDerived(List<ColorClass> classes, List<ColorClass> derived, RgbColor background, IReadOnlyList<RgbColor> palette)
    {
        var placed = classes.Where(c => c.Output != null).ToList();

        // The theme's own range: new colors are no harder to read, no lighter or darker and no more saturated.
        var themeColors = placed.Select(c => c.Output!.Value).Concat(palette).Distinct().ToList();
        var themeLabs = themeColors.Select(OkLab.FromRgb).ToList();
        double minContrast = Math.Clamp(placed.Min(c => OkLab.Contrast(c.Output!.Value, background)), 3.0, 4.5);
        double minL = themeLabs.Min(l => l.L), maxL = themeLabs.Max(l => l.L);
        double maxChroma = themeLabs.Max(l => l.Chroma);

        foreach (var c in derived)
        {
            var basis = c.Basis!;
            var transferred = Transfer(basis.ThemeLab, basis.VisualStudioLab, c.VisualStudioLab);
            var target = OkLab.FromRgb(Clamp(transferred, minL, maxL, maxChroma).ToRgb());

            var snapped = themeColors
                .Select(p => (Color: p, Distance: OkLab.FromRgb(p).DistanceTo(target)))
                .Where(p => p.Distance <= PaletteSnap && IsValid(p.Color))
                .OrderBy(p => p.Distance)
                .Select(p => (RgbColor?)p.Color)
                .FirstOrDefault();
            if (snapped != null)
                Place(snapped.Value, AdaptOrigin.Palette);
            else if (IsValid(target.ToRgb()))
                Place(target.ToRgb(), AdaptOrigin.Derived);
            else
            {
                double hue = target.Chroma < Achromatic ? basis.ThemeLab.Hue : target.Hue;
                Place(AlongDelta() ?? Search(target, hue) ?? target.ToRgb(), AdaptOrigin.Adjusted);
            }

            // Less of Visual Studio's difference: stays a shade of the theme color rather than a new hue.
            RgbColor? AlongDelta()
            {
                double[] steps = [0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3];
                var delta = transferred - basis.ThemeLab;
                return steps
                    .Select(t => Clamp(basis.ThemeLab + new OkLab(delta.L * t, delta.A * t, delta.B * t), minL, maxL, maxChroma).ToRgb())
                    .Select(rgb => (RgbColor?)rgb)
                    .FirstOrDefault(rgb => IsValid(rgb!.Value));
            }

            void Place(RgbColor color, AdaptOrigin origin)
            {
                c.Output = color;
                c.Origin = origin;
                placed.Add(c);
            }

            bool IsValid(RgbColor color)
            {
                if (OkLab.Contrast(color, background) < minContrast)
                    return false;
                var lab = OkLab.FromRgb(color);
                foreach (var other in placed)
                {
                    // As different as either editor shows them, up to a clearly (or, for the theme, barely) visible step.
                    double required = Math.Max(
                        Math.Min(VisualStudioDifference, c.VisualStudioLab.DistanceTo(other.VisualStudioLab)),
                        Math.Min(ThemeDifference, c.ThemeLab.DistanceTo(other.ThemeLab)));
                    if (lab.DistanceTo(OkLab.FromRgb(other.Output!.Value)) < required)
                        return false;
                }
                return true;
            }

            // The nearest valid color around the target, varying lightness, chroma and hue. A gray target has no hue
            // of its own, so it is tinted with the basis color's hue rather than any hue that happens to fit.
            RgbColor? Search(OkLab from, double hue)
            {
                var candidates = new List<(RgbColor Color, double Distance)>();
                for (double dl = -0.15; dl <= 0.15001; dl += 0.01)
                    for (double dc = -0.08; dc <= 0.08001; dc += 0.01)
                        for (double dh = -30; dh <= 30; dh += 5)
                        {
                            double chroma = Math.Max(0, from.Chroma + dc);
                            var lab = OkLab.FromLch(from.L + dl, chroma, hue + dh);
                            if (lab.L < minL - 0.02 || lab.L > maxL + 0.02 || chroma > maxChroma + 0.02)
                                continue;
                            var rgb = lab.ToRgb();
                            candidates.Add((rgb, OkLab.FromRgb(rgb).DistanceTo(from)));
                        }
                return candidates.OrderBy(x => x.Distance).Select(x => (RgbColor?)x.Color).FirstOrDefault(x => IsValid(x!.Value));
            }
        }
    }

    /// <summary>Below this chroma a color is close enough to gray that its hue means little.</summary>
    private const double Achromatic = 0.03;

    /// <summary>Hue differences up to this many degrees are a shade of the same color; larger ones are another color.</summary>
    private const double SameHueFamily = 30;

    /// <summary>
    /// <paramref name="theme"/> changed the way <paramref name="visualStudio"/> differs from <paramref name="visualStudioBasis"/>:
    /// the same lightness step and chroma scaled by the same ratio, so the color is as light and as saturated as the
    /// theme's colors are. A small hue difference (struct's green next to class's teal) turns the theme's hue by the
    /// same angle; a large one (method's yellow next to class's teal) takes Visual Studio's hue, so the color says what
    /// it says in Visual Studio. Against a gray basis, where ratio and angle mean nothing, the chroma step is added.
    /// </summary>
    internal static OkLab Transfer(OkLab theme, OkLab visualStudioBasis, OkLab visualStudio)
    {
        double l = theme.L + (visualStudio.L - visualStudioBasis.L);
        bool grayBasis = visualStudioBasis.Chroma < Achromatic;
        double chroma = grayBasis
            ? theme.Chroma + (visualStudio.Chroma - visualStudioBasis.Chroma)
            : theme.Chroma * (visualStudio.Chroma / visualStudioBasis.Chroma);
        double turn = (visualStudio.Hue - visualStudioBasis.Hue + 540) % 360 - 180;
        double hue = grayBasis || theme.Chroma < Achromatic || Math.Abs(turn) > SameHueFamily
            ? visualStudio.Hue
            : theme.Hue + turn;
        return OkLab.FromLch(l, Math.Max(0, chroma), hue);
    }

    private static OkLab Clamp(OkLab lab, double minL, double maxL, double maxChroma)
    {
        double chroma = lab.Chroma;
        double scale = chroma > maxChroma && chroma > 0 ? maxChroma / chroma : 1;
        return new OkLab(Math.Clamp(lab.L, minL, maxL), lab.A * scale, lab.B * scale);
    }
}
