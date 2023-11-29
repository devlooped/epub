using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Devlooped.Web;

namespace Devlooped.Epub;

/// <summary>
/// Represents an EPUB publication.
/// </summary>
public class Publication : IDisposable
{
    /// <summary>
    /// Opens an EPUB publication from the specified path.
    /// </summary>
    public static Publication Open(string path) => Open(File.OpenRead(path), false);

    /// <summary>
    /// Opens an EPUB publication from the specified stream.
    /// </summary>
    public static Publication Open(Stream stream) => Open(stream, true);

    /// <summary>
    /// Opens an EPUB publication from the specified stream.
    /// </summary>
    static Publication Open(Stream stream, bool leaveOpen) => new(new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen));

    readonly ZipArchive zip;
    Package? package;

    Publication(ZipArchive zip) => this.zip = zip;

    /// <summary>
    /// Disposes the EPUB publication and its inner archive.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizes the EPUB publication and its inner archive.
    /// </summary>
    ~Publication() => Dispose(false);

    /// <summary>
    /// Disposes the EPUB publication and its inner archive.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            zip.Dispose();
    }

    /// <summary>
    /// Gets the EPUB package.
    /// </summary>
    public Package Package => package ??= ReadPackage();

    Package ReadPackage()
    {
        var entry = zip.GetEntry("META-INF/container.xml") ??
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingContainer);

        using var reader = XmlReader.Create(entry.Open()).IgnoreNamespaces();
        var doc = XDocument.Load(reader);
        var root = doc.CssSelectElement("rootfile[full-path][media-type=application/oebps-package+xml]")
            ?? throw new InvalidArchiveException(ThisAssembly.Strings.MissingPackage);

        var path = root.Attribute("full-path")?.Value;
        if (string.IsNullOrEmpty(path))
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingPackage);

        var pe = zip.GetEntry(path) ??
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingPackage);

        using var pr = XmlReader.Create(pe.Open());

        return new Package(zip, XDocument.Load(pr), Path.GetDirectoryName(path) ?? "");
    }
}
