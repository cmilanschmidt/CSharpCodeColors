namespace CSharpCodeColors.Models.Colors;

/// <summary>
/// A color in OKLab (Björn Ottosson, 2020), a perceptual space: equal distances look about equally different,
/// and lightness (L, 0..1) is independent of hue. Chroma and hue are the polar form of (A, B).
/// </summary>
internal readonly record struct OkLab(double L, double A, double B)
{
    public double Chroma => Math.Sqrt(A * A + B * B);

    /// <summary>Hue angle in degrees, 0..360.</summary>
    public double Hue => (Math.Atan2(B, A) * 180 / Math.PI + 360) % 360;

    public static OkLab FromLch(double l, double chroma, double hueDegrees)
    {
        double h = hueDegrees * Math.PI / 180;
        return new OkLab(l, chroma * Math.Cos(h), chroma * Math.Sin(h));
    }

    public static OkLab operator +(OkLab x, OkLab y) => new(x.L + y.L, x.A + y.A, x.B + y.B);

    public static OkLab operator -(OkLab x, OkLab y) => new(x.L - y.L, x.A - y.A, x.B - y.B);

    /// <summary>
    /// How different two colors look: about 0.02 is barely visible between text colors, 0.1 is clearly different.
    /// Euclidean distance after Ottosson's lightness "toe" (from Okhsl), with chroma scaled along: plain OKLab
    /// exaggerates differences near black, where #000001 would be as far from #000000 as two distinct grays.
    /// </summary>
    public double DistanceTo(OkLab other)
    {
        var (l1, a1, b1) = Perceived(this);
        var (l2, a2, b2) = Perceived(other);
        double dl = l1 - l2, da = a1 - a2, db = b1 - b2;
        return Math.Sqrt(dl * dl + da * da + db * db);

        static (double L, double A, double B) Perceived(OkLab c)
        {
            const double k1 = 0.206, k2 = 0.03, k3 = (1 + k1) / (1 + k2);
            double x = k3 * c.L - k1;
            double lr = (x + Math.Sqrt(x * x + 4 * k2 * k3 * c.L)) / 2;
            double scale = c.L > 1e-9 ? lr / c.L : 0;
            return (lr, c.A * scale, c.B * scale);
        }
    }

    public static OkLab FromRgb(RgbColor color)
    {
        double r = ToLinear(color.R), g = ToLinear(color.G), b = ToLinear(color.B);
        double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return new OkLab(
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    /// <summary>
    /// The nearest displayable color: outside the sRGB gamut, chroma is reduced (keeping lightness and hue)
    /// until it fits, which keeps the color's character better than clipping each channel.
    /// </summary>
    public RgbColor ToRgb()
    {
        var lab = this with { L = Math.Clamp(L, 0, 1) };
        if (!lab.TryToLinear(out var linear))
        {
            double low = 0, high = 1, hue = Hue, chroma = Chroma;
            for (int i = 0; i < 24; i++)
            {
                double mid = (low + high) / 2;
                if (FromLch(lab.L, chroma * mid, hue).TryToLinear(out _))
                    low = mid;
                else
                    high = mid;
            }
            FromLch(lab.L, chroma * low, hue).TryToLinear(out linear);
        }
        return new RgbColor(ToByte(linear.R), ToByte(linear.G), ToByte(linear.B));
    }

    public bool IsInGamut => TryToLinear(out _);

    private bool TryToLinear(out (double R, double G, double B) linear)
    {
        double l = L + 0.3963377774 * A + 0.2158037573 * B;
        double m = L - 0.1055613458 * A - 0.0638541728 * B;
        double s = L - 0.0894841775 * A - 1.2914855480 * B;
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;
        linear = (
            4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
        const double tolerance = 1e-4;
        return linear.R is >= -tolerance and <= 1 + tolerance
            && linear.G is >= -tolerance and <= 1 + tolerance
            && linear.B is >= -tolerance and <= 1 + tolerance;
    }

    private static double ToLinear(byte channel)
    {
        double c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte ToByte(double linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        double c = linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return (byte)Math.Round(c * 255);
    }

    /// <summary>WCAG 2 contrast ratio, 1..21.</summary>
    public static double Contrast(RgbColor x, RgbColor y)
    {
        double lx = Luminance(x), ly = Luminance(y);
        return (Math.Max(lx, ly) + 0.05) / (Math.Min(lx, ly) + 0.05);
    }

    /// <summary>WCAG 2 relative luminance, 0..1.</summary>
    public static double Luminance(RgbColor color) =>
        0.2126 * ToLinear(color.R) + 0.7152 * ToLinear(color.G) + 0.0722 * ToLinear(color.B);
}
