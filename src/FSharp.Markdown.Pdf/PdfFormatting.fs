module FSharp.Markdown.Pdf

open FSharp.Markdown

open PdfSharp.Pdf
open MigraDoc.DocumentObjectModel
open MigraDoc.Rendering

let inline updateElements f x = 
    let elements = (^a : (member Elements : ParagraphElements) x)
    f elements

let inline addText str          = updateElements (fun elems -> elems.AddText str)
let inline addFormattedText (str : string) = updateElements (fun elems -> elems.AddFormattedText str)
let inline addLineBreak ()      = updateElements (fun elems -> elems.AddLineBreak())
let inline addHyperLink str     = updateElements (fun elems -> elems.AddHyperlink(str, HyperlinkType.Web))

let getEmphasisStyle = function | "Emphasis" | "Strong" -> "VeryStrong" | _ -> "Strong"

let rec inline formatSpan (x : Paragraph) = function
    | Literal(str)    -> x |> addText str     |> ignore
    | HardLineBreak   -> x |> addLineBreak () |> ignore
    | Strong(spans)   -> x.Style <- getEmphasisStyle x.Style
                         formatSpans x spans
    | Emphasis(spans) -> x.Style <- getEmphasisStyle x.Style
                         formatSpans x spans
    | InlineCode(str) -> let fmtTxt = x |> addFormattedText str
                         fmtTxt.Style <- "Code"  // TODO : this doesn't work...
    | DirectLink([ Literal str ], (link, title))
        -> x.Style <- StyleNames.Hyperlink
           x |> addHyperLink link |> addText str |> ignore

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

let defineStyles (document : Document) =
    document.Styles.[StyleNames.Heading1].Font.Size     <- Unit.FromPoint 32.0
    document.Styles.[StyleNames.Heading2].Font.Size     <- Unit.FromPoint 30.0
    document.Styles.[StyleNames.Heading3].Font.Size     <- Unit.FromPoint 26.0
    document.Styles.[StyleNames.Heading4].Font.Size     <- Unit.FromPoint 22.0
    document.Styles.[StyleNames.Heading5].Font.Size     <- Unit.FromPoint 18.0
    document.Styles.[StyleNames.Heading6].Font.Size     <- Unit.FromPoint 14.0
    document.Styles.[StyleNames.Hyperlink].Font.Color   <- Colors.Blue
    document.Styles.AddStyle("Emphasis", StyleNames.Normal).Font.Italic <- true
    document.Styles.AddStyle("Strong", StyleNames.Normal).Font.Bold     <- true    
    document.Styles.AddStyle("VeryStrong", "Strong").Font.Italic        <- true
    
    let codeStyle = document.Styles.AddStyle("Code", StyleNames.Normal)
    codeStyle.ParagraphFormat.Shading.Visible   <- true
    codeStyle.ParagraphFormat.Shading.Color     <- Colors.LightGray

    document

let formatMarkdown (paragraphs : MarkdownParagraphs) = 
    let pdfDocument = new Document() |> defineStyles
    pdfDocument.AddSection() |> ignore

    for paragraph in paragraphs do
        formatParagraph pdfDocument paragraph

    let renderer = PdfDocumentRenderer(false, PdfFontEmbedding.Always)
    renderer.Document <- pdfDocument
    renderer.RenderDocument()
    renderer.PdfDocument