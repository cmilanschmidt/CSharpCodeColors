using System.Text;
using CSharpCodeColors.Constants;
using CSharpCodeColors.Extensions;
using CSharpCodeColors.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CSharpCodeColors.Tests.TestData;

/// <summary>Reads settings from a JSON string through the same configuration binding and validation as Program.cs.</summary>
internal static class TestOptions
{
    public static AppOptions Parse(string json)
    {
        var file = new AppSettingsFile(FileNames.AppSettings);
        IConfiguration configuration;
        try
        {
            configuration = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
        }
        catch (Exception e)
        {
            // What the OnLoadException handler in Program.cs does for appsettings.json.
            file.LoadError = $"Can't load {file.Path}: {e.GetBaseException().Message}";
            configuration = new ConfigurationBuilder().Build();
        }
        using var provider = new ServiceCollection().AddAppOptions(configuration, file).BuildServiceProvider();
        return provider.GetRequiredService<AppOptionsReader>().Read();
    }
}
