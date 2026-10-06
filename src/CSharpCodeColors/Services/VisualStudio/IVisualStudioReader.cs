using CSharpCodeColors.Exceptions;
using CSharpCodeColors.Models.Colors;

namespace CSharpCodeColors.Services.VisualStudio;

/// <summary>Reads the Text Editor Fonts and Colors of the one running Visual Studio 2026 instance.</summary>
internal interface IVisualStudioReader
{
    /// <summary>Connects and reads every item; throws <see cref="FatalException"/> if that isn't possible.</summary>
    VisualStudioColors Read();
}
