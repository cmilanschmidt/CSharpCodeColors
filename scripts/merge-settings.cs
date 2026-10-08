// Merges the "[Theme]" blocks of several settings files the tool wrote into one file, in argument order,
// formatted like the tool's own output. A .NET 10 file-based app, run by generate-presets.ps1:
//   dotnet run merge-settings.cs -- <output> <input>...
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run merge-settings.cs -- <output> <input>...");
    return 1;
}

var root = new JsonObject();
foreach (var input in args[1..])
{
    foreach (var (key, blocks) in JsonNode.Parse(File.ReadAllText(input))!.AsObject())
    {
        if (root[key] is not JsonObject target)
            root[key] = target = new JsonObject();
        foreach (var (block, content) in blocks!.AsObject())
            target[block] = content!.DeepClone();
    }
}

var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(args[0], root.ToJsonString(options) + Environment.NewLine);
return 0;
