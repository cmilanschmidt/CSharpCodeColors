using Microsoft.Extensions.Options;

namespace CSharpCodeColors.Options;

/// <summary>Runs <see cref="AppOptions.Validate"/> when the options are first read.</summary>
internal sealed class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var errors = options.Validate();
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
