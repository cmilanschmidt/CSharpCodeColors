// Sample file exercising every C# classification covered by the default mapping.
/* Block comment */
#define FEATURE
#region Preprocessor text after region
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Math;
using Builder = System.Text.StringBuilder;
#endregion

namespace SampleNamespace.Inner;

/// <summary>
/// A <see cref="Shape"/> with an <c>Area</c> &amp; more.
/// <![CDATA[ cdata text ]]>
/// <!-- doc comment comment -->
/// <?processing instruction?>
/// <a href="https://example.com">link</a>
/// </summary>
/// <typeparam name="T">The type parameter.</typeparam>
public class Shape<T> where T : struct
{
    public const int MaxSides = 0x10;
    private static readonly double s_scale = 1.5e3;
    private int _sides;
    public event EventHandler? Changed;

    public int Sides
    {
        get => _sides;
        set { _sides = value; Changed?.Invoke(this, EventArgs.Empty); }
    }

    public static Shape<T> operator +(Shape<T> left, Shape<T> right) => new() { Sides = left.Sides + right.Sides };

    public static double Scale(T value, int count)
    {
        var local = count * s_scale;
        Shape<T> a = new(), b = new();
        var sum = a + b;
        if (local > MaxSides)
        {
            goto done;
        }
        foreach (var c in "abc") { local += c; }
    done:
        return Abs(local) + sum.Sides + PI;
    }
}

public record class PersonRecord(string Name, int Age);
public record struct PointRecord(int X, int Y);
public struct Point { public int X; }
public interface IShape { double Area(); }
public enum Color { Red, Green = 2 }
public delegate void Notify(string message);
public unsafe struct WithPointer { public int* Pointer; public delegate*<int, void> FunctionPointer; }

public static class Extensions
{
    public static int Twice(this int value) => value * 2;
}

public static class Strings
{
    public static void Demo([StringSyntax(StringSyntaxAttribute.Regex)] string pattern = @"^\d+(?:a|b)*$")
    {
        string regular = "tab\tnewline\n";
        string verbatim = @"C:\path ""quoted""";
        string raw = """
            raw "string" literal
            """;
        int answer = 42.Twice();
        string interpolated = $"answer = {answer:D3} and {regular}";
        char ch = 'x';
        var regex = new Regex(@"^(?<word>\w+)\s*[a-z]+\.(?#comment)$|\b\x41\t");
        string json2 = /*lang=json*/ "{ 'd': new Date(1), 'n': -Infinity, 'u': undefined }";
        string json = /*lang=json*/ """{ "name": "value", "n": 1, "ok": true, "arr": [ null ] } // c""";
        string json3 = /*lang=json*/ "{ \"a\": hello }";
        Color color = Color.Green;
        Notify notify = message => Console.WriteLine(message);
        notify(interpolated + verbatim + raw + ch + color + json + json2 + json3 + regex + pattern);
        int[] array = [1, 2, 3];
        _ = array.Length;
    }
}

/// <summary>Returns <see langword='null'/> sometimes.</summary>
public sealed class Extras
{
    private const int Limit = 3;

    ~Extras() { }

    public static bool IsLimit(object value) => value is (int)Limit && new Builder().Length == 0;

    public static unsafe int Pointers(Point* point)
    {
        int binary = 0b1010_1010;
        int* pointer = &binary;
        int x = point->X + *pointer;
        global::System.Console.WriteLine(x is Shape<int>.MaxSides);
        return x;
    }

    public static async System.Threading.Tasks.Task<int> MoreAsync(int count)
    {
        long big = 1_000_000L;
        var query = from n in new[] { 1, 2 } where n > 1 select n;
        (int First, int Second) tuple = (1, 2);
#pragma warning disable CS0219
        lock (query) { count++; }
        bool flag = true && !false;
        await System.Threading.Tasks.Task.Yield();
        return typeof(Extras) is null ? 0 : count + (int)big + tuple.First + query.Count();
    }
}

#if NEVER_DEFINED
    excluded code is here
#endif
