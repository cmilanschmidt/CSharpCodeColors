using CSharpCodeColors.Models.Commands;
using CSharpCodeColors.Options;
using CSharpCodeColors.Services.Commands;
using CSharpCodeColors.Services.VisualStudio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CSharpCodeColors.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="AppOptions"/> from <paramref name="configuration"/>, rejecting unknown keys, and registers
    /// <see cref="AppOptionsReader"/> to report load and validation errors from <paramref name="file"/>.
    /// </summary>
    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration, AppSettingsFile file)
    {
        services.AddSingleton(file);
        services.AddOptions<AppOptions>()
            .Bind(configuration, binder => binder.ErrorOnUnknownConfiguration = true)
            .PostConfigure(AppOptions.Normalize);
        services.AddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();
        services.AddSingleton<AppOptionsReader>();
        return services;
    }

    /// <summary>Registers the command given on the command line and the hosted service that runs it.</summary>
    public static IServiceCollection AddCommands(this IServiceCollection services, string[] args)
    {
        services.AddSingleton(CommandLine.Parse(args));
        services.AddSingleton<IVisualStudioReader, VisualStudioReader>();
        services.AddHostedService<CommandRunner>();
        return services;
    }
}
