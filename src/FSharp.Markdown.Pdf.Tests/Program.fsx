#r "bin/Debug/PdfSharp.dll"
#r "bin/Debug/PdfSharp.Charting.dll"
#r "bin/Debug/MigraDoc.DocumentObjectModel.dll"
#r "bin/Debug/MigraDoc.Rendering.dll"
#r "bin/Debug/FSharp.Markdown.dll"
#r "bin/Debug/FSharp.Markdown.Pdf.dll"

open System.Diagnostics

open FSharp.Markdown
open FSharp.Markdown.Pdf

open MigraDoc.DocumentObjectModel

let markdownDoc = """
# This is header 1

## This is header 2

### This is header 3

#### This is header 4

##### This is header 5

###### This is header 6

*emphasis (italic)* or _emphasis (italic)_

**strong emphasis (boldface)** or __strong emphasis (boldface)__

***very strong emphasis (italic and boldface)*** or ___very strong emphasis (italic and boldface)___

Some text with `some code` inside

    line 1 of code
    line 2 of code
    line 3 of code

This is [an example](http://example.com/ "Title") inline link.
[This link](http://example.net/) has no title attribute.

See my [About][about] for more details.

> "This entire paragraph of text will be enclosed in an HTML blockquote element.
Blockquote elements are reflowable. You may arbitrarily
wrap the text to your liking, and it will all be parsed
into a single blockquote element."

Powered By ![Alt text1](http://www.pdfsharp.net/wiki/Images/PoweredBy.png)

Powered By ![Alt text2][id]

---

This is a table:

Heading1|Heading2|Heading3
--------|--------|-------
row11|row12|row13
*row2.1*|__row2.2__|***row2.3***

[id]: http://www.pdfsharp.net/wiki/Images/PoweredBy.png "some title"
[about]: /about/ "About title"
"""

let parsed = Markdown.Parse(markdownDoc)
let pdfDoc = formatMarkdown (new Document()) parsed.Paragraphs

let filename = @"C:\temp\markdown.pdf"
pdfDoc.Save(filename)
Process.Start(filename)