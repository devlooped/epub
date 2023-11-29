using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Devlooped.Web;

namespace Devlooped.Epub;

/// <summary>
/// Represents an EPUB package.
/// </summary>
public partial class Package
{
    static readonly Regex isbnLike =
#if NETSTANDARD2_0
        new(@"(\d+-?)+");
#else
        IsbnLike();
#endif

    static readonly XmlNamespaceManager xmlns = new(new NameTable())
    {
        { "opf", Specification.OpfXmlNs },
        { "dc", Specification.DcXmlNs }
    };

    readonly ZipArchive zip;
    readonly XDocument document;
    readonly string baseDir;
    List<Content>? spine;

    internal Package(ZipArchive zip, XDocument document, string baseDir = "OEBPS")
    {
        if (document.Root == null)
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingPackage);

        this.document = document;
        this.zip = zip;
        this.baseDir = baseDir;

        Title = (string)document.XPathEvaluate("string(opf:package/opf:metadata/dc:title)", xmlns);
        Language = (string)document.XPathEvaluate("string(opf:package/opf:metadata/dc:language)", xmlns);

        if (string.IsNullOrEmpty(Title))
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingTitle);
        if (string.IsNullOrEmpty(Language))
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingLanguage);

        // Identifier: https://idpf.org/epub/30/spec/epub30-publications.html#sec-opf-metadata-identifiers-pid
        var uid = document.Root.Attribute("unique-identifier")?.Value;
        var ids = document.XPathSelectElements("opf:package/opf:metadata/dc:identifier", xmlns);
        var pid = (uid == null ?
            ids.FirstOrDefault() :
            ids.FirstOrDefault(x => x.Attribute("id")?.Value == uid)) 
            ?? throw new InvalidArchiveException(ThisAssembly.Strings.MissingIdentifier);

        var date = document.XPathSelectElement("opf:package/opf:metadata/opf:meta[@property='dcterms:modified']", xmlns)?.Value;
        date ??= document.XPathSelectElement("opf:package/opf:metadata/dc:date[@opf:event='modification']", xmlns)?.Value;

        var dtparse = true;

#if NET6_0_OR_GREATER
        if (DateOnly.TryParseExact(date, "yyyy-MM-dd", out var dateOnly))
        {
            date = dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("s") + "Z";
            dtparse = false;
        }
#endif

        if (dtparse)
        {
            if (DateTime.TryParse(date, out var d))
            {
                // Assume UTC if no timezone/offset is specified.
                if (d.Kind == DateTimeKind.Unspecified)
                    d = DateTime.SpecifyKind(d, DateTimeKind.Utc);

                date = d.ToUniversalTime().ToString("s") + "Z";
            }
            else
                date = null;
        }

        // Append modified date if present.
        if (date != null)
        {
            Identifier = pid.Value + "@" + date;
        }
        else
        {
            Identifier = pid.Value;
        }

        ISBN = ids.Select(x =>
            (isbnLike.IsMatch(x.Value) && ISBN.TryParse(isbnLike.Match(x.Value).Captures[0].Value, out var isbn)) ?
            isbn : null).FirstOrDefault(x => x != null);

        Source = (
            document.XPathSelectElement("opf:package/opf:metadata/dc:source", xmlns) is XElement src &&
            isbnLike.IsMatch(src.Value) &&
            ISBN.TryParse(isbnLike.Match(src.Value).Captures[0].Value, out var isbn)) ?
            isbn : null;
    }

    /// <summary>
    /// Gets the title of the publication.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the identifier of the publication.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Gets the language of the publication.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets the <see cref="Identifier"/> parsed as an <see cref="ISBN"/>, if possible.
    /// </summary>
    public ISBN? ISBN { get; }

    /// <summary>
    /// If a <c>dc:source</c> is present and it can be parsed as an <see cref="ISBN"/>, 
    /// it will typically contain the identifier of the non-ebook format of the same 
    /// publication.
    /// </summary>
    public ISBN? Source { get; }

    /// <summary>
    /// Gets the navigation contents of the publication.
    /// </summary>
    public ICollection<Content> Navigation => spine ??= ReadNavigation();

    List<Content> ReadNavigation()
    {
        var spine = document.XPathSelectElement("opf:package/opf:spine", xmlns)
            ?? throw new InvalidArchiveException(ThisAssembly.Strings.MissingSpine);

        var nav = document.XPathSelectElement("opf:package/opf:manifest/opf:item[@properties='nav']", xmlns);
        var items = document.XPathSelectElements("opf:package/opf:manifest/opf:item", xmlns)
            .Where(x => x.Attribute("id") != null)
#if NET6_0_OR_GREATER
            .DistinctBy(x => x.Attribute("id")!.Value)
#else
            .Distinct(ElementIdAttributeComparer.Default)
#endif
            .ToDictionary(x => x.Attribute("id")!.Value);

        var contents = new List<Content>();

        if (nav != null &&
            nav.Attribute("href")?.Value is string navref &&
            ResolveHref(navref) is Uri navuri &&
            zip.GetEntry(navuri.PathAndQuery.TrimStart('/')) is ZipArchiveEntry naventry)
        {
            using var navstream = naventry.Open();
            using var navreader = XmlReader
                .Create(navstream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore })
                .IgnoreNamespaces();

            // See nav doc schema: https://idpf.org/epub/30/schema/epub-nav-30.sch
            var navdoc = XDocument.Load(navreader);
            var navtoc = (navdoc.Root?.CssSelectElement("nav[type=toc]"))
                ?? throw new InvalidArchiveException(ThisAssembly.Strings.MissingNavigationToc);

            foreach (var link in navtoc.CssSelectElements("li > a[href]"))
            {
                contents.Add(new Content(zip, link.Value.Trim(),
                    ResolveHref(link.Attribute("href")!.Value).PathAndQuery.TrimStart('/')));
            }
        }
        else if (spine?.Attribute("toc")?.Value is string tocid &&
            items.TryGetValue(tocid, out var tocitem) &&
            tocitem.Attribute("href")?.Value is string tocref &&
            ResolveHref(tocref) is Uri tocuri &&
            zip.GetEntry(tocuri.PathAndQuery.TrimStart('/')) is ZipArchiveEntry tocentry)
        {
            using var tocstream = tocentry.Open();
            using var tocreader = XmlReader
                .Create(tocstream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore })
                .IgnoreNamespaces();

            var tocdoc = XDocument.Load(tocreader);

            // no nav, we'll use the spine augmented with legacy toc in
            // ncx format, to retrieve titles for each doc for use.
            var itemRefs = spine.Elements()
                .Select(x =>
                    (x.Attribute("idref")?.Value is string id &&
                    items.TryGetValue(id, out var item)) ? item : null)
                .Where(x => x != null)
                .Select(x => x!)
                .ToList();

            foreach (var item in itemRefs)
            {
                var content = tocdoc.CssSelectElements($"navPoint content[src^='{item.Attribute("href")!.Value}]'")
                    .FirstOrDefault();

                if (content == null ||
                    content.Parent.CssSelectElement("text") is not XElement label)
                    continue;

                contents.Add(new Content(zip, label.Value.Trim(),
                    ResolveHref(item.Attribute("href")!.Value).PathAndQuery.TrimStart('/')));
            }
        }
        else
        {
            throw new InvalidArchiveException(ThisAssembly.Strings.MissingNavigation);
        }

        return contents;
    }

    Uri ResolveHref(string href) => new(new Uri("https://devlooped.com"), new Uri(baseDir + "/" + href, UriKind.Relative));

#if NETSTANDARD2_0
    class ElementIdAttributeComparer : IEqualityComparer<XElement>
    {
        public static IEqualityComparer<XElement> Default { get; } = new ElementIdAttributeComparer();
        ElementIdAttributeComparer() { }
        public bool Equals(XElement x, XElement y) => x.Attribute("id")?.Value == y.Attribute("id")?.Value;
        public int GetHashCode(XElement obj) => obj.Attribute("id")?.Value.GetHashCode() ?? 0;
    }
#endif

#if NET6_0_OR_GREATER
    [GeneratedRegex(@"(\d+-?)+")]
    private static partial Regex IsbnLike();
#endif
}
