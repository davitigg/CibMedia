using System.Reflection;

namespace CibMedia.Core.Presentation.Setup;

// Shipped as an embedded .html with nothing substituted into it, so it stays a file a browser
// can open and a person can edit.
public static class SetupPage
{
    private const string Resource = "CibMedia.Core.Presentation.Setup.Setup.html";

    private static readonly Lazy<string> Content = new(Read);

    public static string Html => Content.Value;

    private static string Read()
    {
        using var stream = typeof(SetupPage).GetTypeInfo().Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"{Resource} is not in the assembly.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
