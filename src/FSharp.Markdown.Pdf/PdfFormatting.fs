module FSharp.Markdown.Pdf

open System
open System.Net
open System.IO

open FSharp.Markdown

open PdfSharp.Pdf
open MigraDoc.DocumentObjectModel
open MigraDoc.Rendering

/// Constant style names so that the user has the option to provide his own styling
/// for a given PDF document
module MarkdownStyleNames = 
    [<Literal>] 
    let Normal        = "MdNormal"
    [<Literal>] 
    let Heading1      = "MdHeading1"
    [<Literal>] 
    let Heading2      = "MdHeading2"
    [<Literal>] 
    let Heading3      = "MdHeading3"
    [<Literal>] 
    let Heading4      = "MdHeading4"
    [<Literal>] 
    let Heading5      = "MdHeading5"
    [<Literal>] 
    let Heading6      = "MdHeading6"
    [<Literal>] 
    let Hyperlink     = "MdHyperlink"
    [<Literal>] 
    let Emphasis      = "MdEmphasis"
    [<Literal>] 
    let Strong        = "MdStrong"
    [<Literal>] 
    let VeryStrong    = "MdVeryStrong"
    [<Literal>] 
    let Quoted        = "MdQuoted"
    [<Literal>] 
    let Code          = "MdCode"

type Context = 
    { 
        Document        : Document
        StyleOverride   : string option
    }

/// Sets the default styles if user-provided overrides do not exist
let setDefaultStyles (document : Document) =
    let setIfNotExist styleName baseStyleName update =
        if not <| document.Styles.HasValue styleName then
            document.Styles.AddStyle(styleName, baseStyleName) |> update

    setIfNotExist MarkdownStyleNames.Heading1 StyleNames.Heading1 
                  (fun style -> style.Font.Size <- Unit.FromPoint 32.0)
    setIfNotExist MarkdownStyleNames.Heading2 StyleNames.Heading2
                  (fun style -> style.Font.Size <- Unit.FromPoint 28.0)
    setIfNotExist MarkdownStyleNames.Heading3 StyleNames.Heading3
                  (fun style -> style.Font.Size <- Unit.FromPoint 24.0)
    setIfNotExist MarkdownStyleNames.Heading4 StyleNames.Heading4
                  (fun style -> style.Font.Size <- Unit.FromPoint 20.0)
    setIfNotExist MarkdownStyleNames.Heading5 StyleNames.Heading5
                  (fun style -> style.Font.Size <- Unit.FromPoint 16.0)
    setIfNotExist MarkdownStyleNames.Heading6 StyleNames.Heading6
                  (fun style -> style.Font.Size <- Unit.FromPoint 12.0)
    setIfNotExist MarkdownStyleNames.Normal StyleNames.Normal
                  (fun style -> style.Font.Size <- Unit.FromPoint 10.0)

    setIfNotExist MarkdownStyleNames.Hyperlink StyleNames.Hyperlink
                  (fun style -> style.Font.Color <- Colors.Blue)

    setIfNotExist MarkdownStyleNames.Emphasis MarkdownStyleNames.Normal
                  (fun style -> style.Font.Italic <- true)
    setIfNotExist MarkdownStyleNames.Strong MarkdownStyleNames.Normal
                  (fun style -> style.Font.Bold <- true)
    setIfNotExist MarkdownStyleNames.VeryStrong MarkdownStyleNames.Strong
                  (fun style -> style.Font.Italic <- true)

    setIfNotExist MarkdownStyleNames.Quoted MarkdownStyleNames.Normal
                  (fun style -> style.ParagraphFormat.Borders.DistanceFromLeft  <- Unit.FromPoint 17.0
                                style.ParagraphFormat.LeftIndent        <- Unit.FromPoint 20.0
                                style.ParagraphFormat.Borders.Left      <- Border(Color = Colors.LightGray, Width = Unit.FromPoint 3.0)
                                style.ParagraphFormat.Font.Color        <- Colors.Gray)
    setIfNotExist MarkdownStyleNames.Code MarkdownStyleNames.Normal
                  (fun style -> style.ParagraphFormat.Borders.Distance  <- Unit.FromPoint 4.0
                                style.ParagraphFormat.LeftIndent        <- Unit.FromPoint 5.0
                                style.ParagraphFormat.Shading.Visible   <- true
                                style.ParagraphFormat.Shading.Color     <- Colors.LightGray
                                style.ParagraphFormat.Borders.Width     <- Unit.FromPoint 1.0
                                style.ParagraphFormat.Borders.Color     <- Colors.Gray)

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

let emphasis = function | MarkdownStyleNames.Emphasis, MarkdownStyleNames.Strong 
                        | MarkdownStyleNames.Strong, MarkdownStyleNames.Emphasis 
                                      -> MarkdownStyleNames.VeryStrong
                        | newStyle, _ -> newStyle

let downloadImg (link : string) =
    let localPath = Path.GetTempFileName() + Path.GetExtension link
    use client = new WebClient()
    client.DownloadFile(link, localPath)
    localPath

let rec inline formatSpan (x : Paragraph) = function
    | Literal(str)    -> x |> addText str     |> ignore
    | HardLineBreak   -> x |> addLineBreak () |> ignore
    | Strong(spans)   -> x.Style <- emphasis (MarkdownStyleNames.Strong, x.Style)
                         formatSpans x spans
    | Emphasis(spans) -> x.Style <- emphasis (MarkdownStyleNames.Emphasis, x.Style)
                         formatSpans x spans
    | InlineCode(str) -> x |> addFormattedTextWithStyle str MarkdownStyleNames.Code |> ignore // TODO : this doesn't work
    | DirectLink([ Literal str ], (link, _))
        -> x 
           |> addHyperLink link
           |> addFormattedTextWithStyle str MarkdownStyleNames.Hyperlink |> ignore
    | IndirectLink([ Literal str ], original, _)
        -> () // TODO
    | DirectImage(altText, (link, _))
        -> let localFile = downloadImg link
           x.AddImage(localFile) |> ignore
    | IndirectImage(altText, link, title)
        -> () // TODO

and formatSpans x = List.iter (formatSpan x)

let rec formatParagraph (cxt : Context) (mdParagraph : MarkdownParagraph) =
    let pdfParagraph = cxt.Document.LastSection.AddParagraph()
    match cxt.StyleOverride with
    | Some styleName -> pdfParagraph.Style <- styleName
    | _ -> ()
    
    match mdParagraph with
    | Heading(n, spans)      -> pdfParagraph.Style <- "MdHeading" + string n
                                formatSpans pdfParagraph spans
    | Paragraph(spans)       
    | Span(spans)            -> formatSpans pdfParagraph spans
    | CodeBlock(str)         -> pdfParagraph.Style <- MarkdownStyleNames.Code
                                pdfParagraph.AddFormattedText(str) |> ignore
    | HtmlBlock _            -> raise <| NotSupportedException()
    | ListBlock _            -> () // TODO
    | QuotedBlock paragraphs -> 
        let cxt = { cxt with StyleOverride = Some MarkdownStyleNames.Quoted }
        formatParagraphs cxt paragraphs
    | HorizontalRule         ->
        // not sure if it's the right choice, but let's interpret horizontal rule as a page break
        cxt.Document.AddSection() |> ignore
    | TableBlock(headers, alignments, rows)
        -> () // TODO

and formatParagraphs (cxt : Context) = List.iter (formatParagraph cxt)

let formatMarkdown (document : Document) (paragraphs : MarkdownParagraphs) = 
    setDefaultStyles document
    document.AddSection() |> ignore

    let cxt = { Document = document; StyleOverride = None }
    
    formatParagraphs cxt paragraphs

    let renderer = PdfDocumentRenderer(false, PdfFontEmbedding.Always)
    renderer.Document <- document
    renderer.RenderDocument()
    renderer.PdfDocument