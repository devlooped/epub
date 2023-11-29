using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Devlooped.Web;
using Xunit;
using Xunit.Abstractions;

namespace Devlooped.Epub;

public sealed class PublicationTests : IDisposable
{
    readonly ZipArchive zip;

    public PublicationTests(ITestOutputHelper output)
    {
        Output = output;
        zip = new ZipArchive(File.OpenRead(@"Samples/how-to-think-about-the-economy.epub"));
    }

    public ITestOutputHelper Output { get; }

    public void Dispose() => zip.Dispose();

    [Fact]
    public void CanOpen()
    {
        using var pub = Publication.Open(@"Samples/how-to-think-about-the-economy.epub");

        Assert.NotNull(pub);
    }

    [Fact]
    public void CanAccessPackageInformation()
    {
        using var pub = Publication.Open(@"Samples/how-to-think-about-the-economy.epub");

        Assert.Equal("How to Think about the Economy", pub.Package.Title);
        Assert.Equal("en-US", pub.Package.Language);
    }

    [Fact]
    public void PackageWithSingleIdentifier()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package version="2.0" xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                <dc:title>EPUB 3.0</dc:title>
                <dc:language>en-US</dc:language>
                <dc:identifier>urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809</dc:identifier>
              </metadata>
            </package>
            """;

        var package = new Package(zip, XDocument.Parse(xml));

        Assert.Equal("urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809", package.Identifier);
    }

    [Fact]
    public void PackageWithMultipleIdentifiersGetsFirst()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package version="2.0" xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                <dc:title>EPUB 3.0</dc:title>
                <dc:language>en-US</dc:language>
                <dc:identifier>urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809</dc:identifier>
                <dc:identifier id="uid">9781250313171</dc:identifier>
              </metadata>
            </package>
            """;

        var package = new Package(zip, XDocument.Parse(xml));

        Assert.Equal("urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809", package.Identifier);
    }

    [Fact]
    public void PackageWithMultipleIdentifiersGetsDesignatedUniqueId()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package version="2.0" unique-identifier="uid" xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                <dc:title>EPUB 3.0</dc:title>
                <dc:language>en-US</dc:language>
                <dc:identifier>urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809</dc:identifier>
                <dc:identifier id="uid">D64580F3-7389-4EE4-BD5B-0ECBFE076035</dc:identifier>
              </metadata>
            </package>
            """;

        var package = new Package(zip, XDocument.Parse(xml));

        Assert.Equal("D64580F3-7389-4EE4-BD5B-0ECBFE076035", package.Identifier);
    }

    [Fact]
    public void PackageWithModifiedMetaGetsDateInIdentifier()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package version="2.0" xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                <dc:title>EPUB 3.0</dc:title>
                <dc:language>en-US</dc:language>
                <dc:identifier>urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809</dc:identifier>
                <meta property="dcterms:modified">2011-01-01T12:00:00Z</meta>
              </metadata>
            </package>
            """;

        var package = new Package(zip, XDocument.Parse(xml));

        Assert.Equal("urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809@2011-01-01T12:00:00Z", package.Identifier);
    }

    [Fact]
    public void PackageWithModifiedEventGetsDateInIdentifier()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package version="2.0" xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                <dc:title>EPUB 3.0</dc:title>
                <dc:language>en-US</dc:language>
                <dc:identifier>urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809</dc:identifier>
                <dc:date opf:event="modification" xmlns:opf="http://www.idpf.org/2007/opf">2022-08-18</dc:date>
              </metadata>
            </package>
            """;

        var package = new Package(zip, XDocument.Parse(xml));

        Assert.Equal("urn:uuid:A1B0D67E-2E81-4DF5-9E67-A64CBE366809@2022-08-18T00:00:00Z", package.Identifier);
    }


    [Fact]
    public void PackageWithISBN()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package version="2.0" xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                <dc:title>EPUB 3.0</dc:title>
                <dc:language>en-US</dc:language>
                <dc:identifier>978-1-61016-755-0</dc:identifier>
                <dc:date opf:event="modification" xmlns:opf="http://www.idpf.org/2007/opf">2022-08-18</dc:date>
              </metadata>
            </package>
            """;

        var package = new Package(zip, XDocument.Parse(xml));

        Assert.Equal("9781610167550", package.ISBN?.CanonicalNumber);
    }

    [Fact]
    public void PackageWithSource()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package version="2.0" xmlns="http://www.idpf.org/2007/opf">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">
                <dc:title>EPUB 3.0</dc:title>
                <dc:language>en-US</dc:language>
                <dc:identifier>978-1-61016-755-0</dc:identifier>
                <dc:date opf:event="modification" xmlns:opf="http://www.idpf.org/2007/opf">2022-08-18</dc:date>
                <dc:source id="src-id">urn:isbn:9780375704024</dc:source>
              </metadata>
            </package>
            """;

        var package = new Package(zip, XDocument.Parse(xml));

        Assert.Equal("9780375704024", package.Source?.CanonicalNumber);
    }

    [Fact]
    public void PackageWithSpine()
    {
        using var pub = Publication.Open(@"Samples/how-to-think-about-the-economy.epub");

        Assert.NotNull(pub.Package.Navigation);
        Assert.NotEmpty(pub.Package.Navigation);
    }

    [Fact]
    public void OpenDocFromSpineToc()
    {
        using var pub = Publication.Open(@"Samples/how-to-think-about-the-economy.epub");

        var doc = pub.Package.Navigation.First().Open();

        Assert.Equal("How to Think about the Economy", doc.CssSelectElement("head title")?.Value);
    }

    [Fact]
    public void OpenDocFromSpineNav()
    {
        using var pub = Publication.Open(@"Samples/moby-dick-mo.epub");

        var doc = pub.Package.Navigation.First().Open();

        Assert.Equal("Moby-Dick", doc.CssSelectElement("head title")?.Value);
    }

    [Fact]
    public void PackageWithContent()
    {
        using var pub = Publication.Open(@"Samples/la-telarania-de-carlota.epub");

        Assert.NotNull(pub.Package.Navigation);
        Assert.NotEmpty(pub.Package.Navigation);

        var ch01 = pub.Package.Navigation.First(c => c.Title.StartsWith("I.")).Open();
        var para = ch01.CssSelectElements("p");
        var builder = new StringBuilder();
        foreach (var p in para)
        {
            builder.AppendLine(p.Value).AppendLine();
        }

        Output.WriteLine(builder.ToString());
    }
}
