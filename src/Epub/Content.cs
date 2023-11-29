using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Devlooped.Epub;

/// <summary>
/// Represents a content document in the EPUB archive.
/// </summary>
public class Content
{
    readonly ZipArchive zip;

    internal Content(ZipArchive zip, string title, string href)
        => (this.zip, Title, Href)
        = (zip, title, href);

    /// <summary>
    /// Gets the title of the content document.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the relative path to the content document.
    /// </summary>
    public string Href { get; }

    /// <summary>
    /// Opens the content document as an <see cref="XDocument"/>.
    /// </summary>
    /// <exception cref="InvalidArchiveException">Epub archive is invalid.</exception>
    public XDocument Open()
    {
        var entry = zip.GetEntry(Href) ??
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingContent(Href));

        using var stream = entry.Open();
        using var reader = XmlReader
            .Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore })
            .IgnoreNamespaces();

        return XDocument.Load(reader);
    }

    /// <summary>
    /// Returns a string representation of the content document.
    /// </summary>
    public override string ToString() => $"{Title} ({Href})";
}
