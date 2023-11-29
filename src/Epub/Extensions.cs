using System.Xml;

namespace Devlooped.Epub;

static class Extensions
{
    public static void Add(this XmlNamespaceManager xmlns, string prefix, string uri)
        => xmlns.AddNamespace(prefix, uri);
}
