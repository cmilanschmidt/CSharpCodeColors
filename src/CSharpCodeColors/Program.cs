using CSharpCodeColors.Constants;
using CSharpCodeColors.Extensions;
using CSharpCodeColors.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

// An empty builder: no environment variables, command-line configuration or appsettings.{Environment}.json.
var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });

// Settings: appsettings.jsonc next to the executable (the default content root). It is bound from a configuration
// of its own, so that keys the host adds (such as contentRoot) don't count as unknown settings. A load error
// doesn't stop the host from building; AppOptionsReader reports it through logging like every other error.
var settingsFile = new AppSettingsFile(Path.Combine(builder.Environment.ContentRootPath, FileNames.AppSettings));
var settingsConfiguration = new ConfigurationBuilder()
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile(source =>
    {
        source.Path = FileNames.AppSettings;
        source.OnLoadException = context =>
        {
            settingsFile.LoadError = $"Can't load {settingsFile.Path}: {context.Exception.GetBaseException().Message}";
            context.Ignore = true;
        };
    })
    .Build();

builder.Services.AddAppOptions(settingsConfiguration, settingsFile);
builder.Logging.AddPlainConsole();
builder.Services.AddCommands(args);

// CommandRunner sets Environment.ExitCode and stops the host when the command is done.
await builder.Build().RunAsync();
