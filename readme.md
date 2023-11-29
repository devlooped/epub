![Icon](https://raw.githubusercontent.com/devlooped/epub/main/assets/img/icon.png) EPUB
============

Lightweight read-only API for processing EPUB documents.

[![Version](https://img.shields.io/nuget/v/Devlooped.Epub.svg?color=royalblue)](https://www.nuget.org/packages/Devlooped.Epub)
[![Downloads](https://img.shields.io/nuget/dt/Devlooped.Epub.svg?color=green)](https://www.nuget.org/packages/Devlooped.Epub)
[![License](https://img.shields.io/github/license/devlooped/epub.svg?color=blue)](https://github.com/devlooped/epub/blob/main/license.txt)
[![Build](https://github.com/devlooped/epub/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/epub/actions)

<!-- #content -->
## Usage

```csharp
using var pub = Publication.Open(@"Samples/how-to-think-about-the-economy.epub");

Assert.Equal("How to Think about the Economy", pub.Package.Title);
Assert.Equal("en-US", pub.Package.Language);

foreach (var content in pub.Package.Navigation)
{
    XDocument doc = content.Open();

    // Useful in conjunction with Devlooped.Web for CSS-selectors
    string? title = doc.CssSelectElement("head title")?.Value;
    // ...
}
```

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->