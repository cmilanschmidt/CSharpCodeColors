namespace CSharpCodeColors.Exceptions;

/// <summary>An error that ends the run with a message and a non-zero exit code.</summary>
internal sealed class FatalException(string message) : Exception(message);
