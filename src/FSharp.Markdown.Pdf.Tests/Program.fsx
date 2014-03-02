#r "bin/PdfSharp.dll"
#r "bin/PdfSharp.Charting.dll"
#r "bin/MigraDoc.DocumentObjectModel.dll"
#r "bin/MigraDoc.Rendering.dll"
#r "bin/FSharp.Markdown.dll"
#r "bin/FSharp.Markdown.Pdf.dll"

open System.Diagnostics

open FSharp.Markdown
open FSharp.Markdown.Pdf

open PdfSharp.Pdf
open MigraDoc.DocumentObjectModel
open MigraDoc.Rendering

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

* An item in a bulleted (unordered) list
    + A subitem, indented with 4 spaces
        - A sub subitem, indented with another 4 spaces
- Another item in a bulleted list
* Here's another item

another list

1. An item in an enumerated (ordered) list
    1. A subitem, indented with 4 spaces
        1. A sub subitem, indented with another 4 spaces
2. Another item in an enumerated list
9. Another item
5. Yet another item


This is [an example](http://example.com/ "Title") inline link.
[This link](http://example.net/) has no title attribute.

See my [About][about] for more details.
See my [about][] for more details.

> "This *entire* __paragraph__ ***of*** text will be enclosed in an HTML blockquote element.
Blockquote elements are reflowable. You may arbitrarily
wrap the text to your liking, and it will all be parsed
into a single blockquote element."

Powered By ![Alt text1](http://www.pdfsharp.net/wiki/Images/PoweredBy.png)

Powered By ![Alt text2][id]

---

This is a table:

Heading1|Heading2|Heading3|Heading4
--------|:--------:|-------:|:-------
row11|row12|row13|
*row2.1*|__row2.2__|***row2.3***|row2.4

[id]: http://www.pdfsharp.net/wiki/Images/PoweredBy.png "some title"
[about]: /about/ "About title"
"""

let parsed = Markdown.Parse(markdownDoc)
let pdfDoc = formatMarkdown (new Document()) parsed.DefinedLinks parsed.Paragraphs

let filename = @"C:\temp\markdown.pdf"
pdfDoc.Save(filename)
Process.Start(filename)

Markdown.TransformPdf(markdownDoc, @"C:\temp\markdown2.pdf")
Process.Start(@"C:\temp\markdown2.pdf")

open System.IO

let stream = File.OpenWrite(@"C:\temp\markdown3.pdf")
Markdown.TransformPdf(markdownDoc, stream)
stream.Close()
Process.Start(@"C:\temp\markdown3.pdf")

MarkdownPdf.Transform(markdownDoc, @"C:\temp\markdown4.pdf")
Process.Start(@"C:\temp\markdown4.pdf")

let stream' = File.OpenWrite(@"C:\temp\markdown5.pdf")
MarkdownPdf.Transform(markdownDoc, stream')
Process.Start(@"C:\temp\markdown5.pdf")

let doc = new Document()
doc.AddSection() |> ignore
let sec = doc.LastSection
MarkdownPdf.AddMarkdown(doc, sec, markdownDoc)

let renderer = PdfDocumentRenderer(false, PdfFontEmbedding.Always)
renderer.Document <- doc
renderer.RenderDocument()
renderer.PdfDocument.Save(@"C:\temp\markdown6.pdf")
Process.Start(@"C:\temp\markdown6.pdf")

let doc2 = new Document()
doc2.AddSection() |> ignore
let sec2 = doc2.LastSection
MarkdownPdf.AddMarkdown(doc2, sec2, parsed)

let renderer2 = PdfDocumentRenderer(false, PdfFontEmbedding.Always)
renderer2.Document <- doc2
renderer2.RenderDocument()
renderer2.PdfDocument.Save(@"C:\temp\markdown7.pdf")
Process.Start(@"C:\temp\markdown7.pdf")


// custom styling
let doc3 = new Document()

let quotedBlockStyle = doc3.Styles.AddStyle(MarkdownStyleNames.Quoted, StyleNames.Normal)
quotedBlockStyle.ParagraphFormat.Shading.Visible    <- true
quotedBlockStyle.ParagraphFormat.Shading.Color      <- Colors.Black
quotedBlockStyle.Font.Color         <- Colors.White

let pdfDoc2 = formatMarkdown doc3 parsed.DefinedLinks parsed.Paragraphs

let filename2 = @"C:\temp\markdown-customized.pdf"
pdfDoc2.Save(filename2)
Process.Start(filename2)