module FSharp.Markdown.Pdf

open System
open System.Net
open System.IO

open FSharp.Markdown

open PdfSharp.Pdf
open MigraDoc.DocumentObjectModel
open MigraDoc.Rendering

let defineStyles (document : Document) =
    document.Styles.[StyleNames.Heading1].Font.Size     <- Unit.FromPoint 32.0
    document.Styles.[StyleNames.Heading2].Font.Size     <- Unit.FromPoint 28.0
    document.Styles.[StyleNames.Heading3].Font.Size     <- Unit.FromPoint 24.0
    document.Styles.[StyleNames.Heading4].Font.Size     <- Unit.FromPoint 20.0
    document.Styles.[StyleNames.Heading5].Font.Size     <- Unit.FromPoint 16.0
    document.Styles.[StyleNames.Heading6].Font.Size     <- Unit.FromPoint 12.0
    document.Styles.[StyleNames.Normal].Font.Size       <- Unit.FromPoint 10.0
    document.Styles.[StyleNames.Hyperlink].Font.Color   <- Colors.Blue
    document.Styles.AddStyle("Emphasis", StyleNames.Normal).Font.Italic <- true
    document.Styles.AddStyle("Strong", StyleNames.Normal).Font.Bold     <- true    
    document.Styles.AddStyle("VeryStrong", "Strong").Font.Italic        <- true
    
    let codeStyle = document.Styles.AddStyle("Code", StyleNames.Normal)
    codeStyle.ParagraphFormat.Shading.Visible   <- true
    codeStyle.ParagraphFormat.Shading.Color     <- Colors.LightGray

    document

let inline updateElements f x = 
    let elements = (^a : (member Elements : ParagraphElements) x)
    f elements

let inline addText str          = updateElements (fun elems -> elems.AddText str)
let inline addLineBreak ()      = updateElements (fun elems -> elems.AddLineBreak())
let inline addHyperLink str     = updateElements (fun elems -> elems.AddHyperlink(str, HyperlinkType.Web))
let inline addImage link        = updateElements (fun elems -> elems.AddImage(link))
let inline addFormattedTextWithFont (str : string) (font : Font) = 
    updateElements (fun elems -> elems.AddFormattedText(str, font))
let inline addFormattedTextWithStyle (str : string) (style : string) = 
    updateElements (fun elems -> elems.AddFormattedText(str, style))

let emphasis = function | "Emphasis", "Strong" | "Strong", "Emphasis" -> "VeryStrong" 
                        | newStyle, _ -> newStyle

let downloadImg (link : string) =
    let localPath = Path.GetTempFileName() + Path.GetExtension link
    use client = new WebClient()
    client.DownloadFile(link, localPath)
    localPath

let rec inline formatSpan (x : Paragraph) = function
    | Literal(str)    -> x |> addText str     |> ignore
    | HardLineBreak   -> x |> addLineBreak () |> ignore
    | Strong(spans)   -> x.Style <- emphasis ("Strong", x.Style)
                         formatSpans x spans
    | Emphasis(spans) -> x.Style <- emphasis ("Emphasis", x.Style)
                         formatSpans x spans
    | InlineCode(str) -> x |> addFormattedTextWithStyle str "Code" |> ignore // TODO : this doesn't work
    | DirectLink([ Literal str ], (link, _))
        -> let hyperlink = x |> addHyperLink link
           let text = hyperlink |> addText str
           hyperlink.Font.Color <- Colors.Blue
    | IndirectLink([ Literal str ], original, _)
        -> () // TODO
    | DirectImage(altText, (link, _))
        -> let localFile = downloadImg link
           x.AddImage(localFile) |> ignore
    | IndirectImage(altText, link, title)
        -> () // TODO

and formatSpans x = List.iter (formatSpan x)

let rec formatParagraph (document : Document) (paragraph : MarkdownParagraph) =
    let pdfParagraph = document.LastSection.AddParagraph()
    
    match paragraph with
    | Heading(n, spans) ->
        pdfParagraph.Style <- "Heading" + string n
        formatSpans pdfParagraph spans
    | Paragraph(spans)
    | Span(spans)       ->
        formatSpans pdfParagraph spans
    | CodeBlock(str)    ->
        pdfParagraph.Style <- "Code"
        pdfParagraph.AddFormattedText(str) |> ignore
    | HtmlBlock _            -> raise <| NotSupportedException()
    | ListBlock _            -> () // TODO
    | QuotedBlock paragraphs -> () // TODO
    | HorizontalRule         -> () // TODO
    | TableBlock _           -> () // TODO

and formatParagraphs (document : Document) = List.iter (formatParagraph document)

let formatMarkdown (paragraphs : MarkdownParagraphs) = 
    let pdfDocument = new Document() |> defineStyles
    pdfDocument.AddSection() |> ignore

    formatParagraphs pdfDocument paragraphs

    let renderer = PdfDocumentRenderer(false, PdfFontEmbedding.Always)
    renderer.Document <- pdfDocument
    renderer.RenderDocument()
    renderer.PdfDocument