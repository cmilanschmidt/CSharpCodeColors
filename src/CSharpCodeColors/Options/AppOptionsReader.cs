using CSharpCodeColors.Exceptions;
using Microsoft.Extensions.Options;

namespace CSharpCodeColors.Options;

/// <summary>Gives the validated settings, or a <see cref="FatalException"/> saying what is wrong with appsettings.json.</summary>
internal sealed class AppOptionsReader(IOptions<AppOptions> options, AppSettingsFile file)
{
    public AppOptions Read()
    {
        if (file.LoadError != null)
            throw new FatalException(file.LoadError);
        try
        {
            return options.Value;
        }
        catch (OptionsValidationException e)
        {
            throw new FatalException($"{file.Path}: invalid settings.{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", e.Failures));
        }
        catch (InvalidOperationException e)
        {
            // The binder wraps the specific error (an unknown property, a value of the wrong type) in a generic one.
            var specific = e;
            while (specific.InnerException is InvalidOperationException inner)
                specific = inner;
            throw new FatalException($"{file.Path}: invalid settings. {specific.Message}");
        }
    }
}
