module FSharp.Markdown.Pdf

open System
open System.Collections.Generic
open System.Net
open System.IO

open FSharp.Markdown

open PdfSharp.Pdf
open MigraDoc.DocumentObjectModel
open MigraDoc.DocumentObjectModel.Tables
open MigraDoc.Rendering

type StyleName = string
type LinkId    = string
type Link      = string
type Title     = string option

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
    let Quoted        = "MdQuoted"
    [<Literal>] 
    let Code          = "MdCode"
    [<Literal>]
    let Table         = "MdTable"

/// Context passed around while formatting the PDF document
type Context = 
    { 
        Document        : Document
        Links           : IDictionary<LinkId, Link * Title>
        StyleOverride   : StyleName option
        BoldOverride    : bool option
        ItalicOverride  : bool option
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
                  (fun style -> style.Font.Bold      <- true
                                style.Font.Color     <- Colors.Blue
                                style.Font.Underline <- Underline.Single)

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

    setIfNotExist MarkdownStyleNames.Table MarkdownStyleNames.Normal (fun style -> ())

/// Helper function for updating the ParagraphElements object part of Paragraph/Hyperlink, etc.
let inline updateElements f x = 
    let elements = (^a : (member Elements : ParagraphElements) x)
    f elements

let inline addLineBreak ()      = updateElements (fun elems -> elems.AddLineBreak())
let inline addHyperLink str     = updateElements (fun elems -> elems.AddHyperlink(str, HyperlinkType.Web))
let inline addImage link        = updateElements (fun elems -> elems.AddImage(link))

let inline addFormattedText { BoldOverride = bold; ItalicOverride = italic } (str : string) = 
    updateElements (fun elems -> 
        let fmtTxt= elems.AddFormattedText str
        match bold with | Some true -> fmtTxt.Font.Bold <- true | _ -> ()
        match italic with | Some true -> fmtTxt.Font.Italic <- true | _ -> ()
        fmtTxt)
let inline addFormattedTextWithFont (font : Font) (str : string) = 
    updateElements (fun elems -> elems.AddFormattedText(str, font))
let inline addFormattedTextWithStyle (styleName : StyleName) (str : string)  = 
    updateElements (fun elems -> elems.AddFormattedText(str, styleName))
    
/// Downloads the image to a local path
let downloadImg (link : Link) =
    let localPath = Path.GetTempFileName() + Path.GetExtension link
    use client = new WebClient()
    client.DownloadFile(link, localPath)
    localPath

/// Lookup a specified key in a dictionary, possibly ignoring newlines or spaces in the key.
let (|LookupKey|_|) (dict:IDictionary<_, _>) (key:string) = 
  [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " "); 
    key.Replace("\n", ""); key.Replace("\n", " ") ]
  |> Seq.tryPick (fun key -> match dict.TryGetValue(key) with | true, v -> Some v | _ -> None)

/// Write MarkdownSpan value to a PDF document
let rec inline formatSpan (ctx : Context) (paragraph : Paragraph) = function
    | Literal(str)    -> paragraph |> addFormattedText ctx str |> ignore
    | HardLineBreak   -> paragraph |> addLineBreak () |> ignore

    | Strong(spans)   -> let ctx = { ctx with BoldOverride = Some true }
                         formatSpans ctx paragraph spans
    | Emphasis(spans) -> let ctx = { ctx with ItalicOverride = Some true }
                         formatSpans ctx paragraph spans
    | InlineCode(str) -> paragraph
                         |> addFormattedTextWithStyle MarkdownStyleNames.Code str 
                         |> ignore // TODO : this doesn't work

    | IndirectLink([ Literal str ], _, LookupKey ctx.Links (link, _))
    | DirectLink([ Literal str ], (link, _))
        -> paragraph
           |> addHyperLink link
           |> addFormattedTextWithStyle MarkdownStyleNames.Hyperlink str |> ignore

    | IndirectImage(altText, _, LookupKey ctx.Links (link, _))
    | DirectImage(altText, (link, _))
        -> let localFile = downloadImg link
           paragraph.AddImage(localFile) |> ignore
    | _ -> ()

/// Write a list of MarkdownSpan values to a PDF document
and formatSpans ctx x = List.iter (formatSpan ctx x)

/// Write a MarkdownParagraph value to a PDF document
let rec formatParagraph (ctx : Context) (addParagraph : unit -> Paragraph) (mdParagraph : MarkdownParagraph) =
    let pdfParagraph = addParagraph()
    match ctx.StyleOverride with
    | Some styleName -> pdfParagraph.Style <- styleName
    | _ -> ()
    
    match mdParagraph with
    | Heading(n, spans)      -> pdfParagraph.Style <- "MdHeading" + string n
                                formatSpans ctx pdfParagraph spans
    | Paragraph(spans)       
    | Span(spans)            -> formatSpans ctx pdfParagraph spans
    
    // treat the raw HTML as 'code', without writing a HTML to PDF renderer ;-)
    | HtmlBlock code
    | CodeBlock(code)        -> pdfParagraph.Style <- MarkdownStyleNames.Code
                                pdfParagraph |> addFormattedText ctx code |> ignore

    | ListBlock _            -> () // TODO
    | QuotedBlock paragraphs -> 
        let ctx = { ctx with StyleOverride = Some MarkdownStyleNames.Quoted }
        formatParagraphs ctx addParagraph paragraphs
    | HorizontalRule         ->
        // not sure if it's the right choice, but let's interpret horizontal rule as a page break
        ctx.Document.LastSection.AddPageBreak()
    | TableBlock(headers, alignments, rows) -> 
        let table = ctx.Document.LastSection.AddTable()
        table.Style              <- MarkdownStyleNames.Table
        table.Borders.Color      <- Colors.LightGray
        table.Borders.Width      <- Unit.FromPoint 0.25
        table.TopPadding         <- Unit.FromPoint 10.0
        table.RightPadding       <- Unit.FromPoint 10.0
        table.BottomPadding      <- Unit.FromPoint 10.0
        table.LeftPadding        <- Unit.FromPoint 10.0

        alignments 
        |> List.map (fun alignment -> alignment, table.AddColumn())
        |> List.iter (function | AlignRight, column   -> column.Format.Alignment <- ParagraphAlignment.Right
                               | AlignCenter, column  -> column.Format.Alignment <- ParagraphAlignment.Center
                               | AlignLeft, column    
                               | AlignDefault, column -> column.Format.Alignment <- ParagraphAlignment.Left)
         
        seq {
           if headers.IsSome then yield headers.Value, { ctx with BoldOverride = Some true }
           yield! rows |> Seq.map (fun row -> row, ctx)
        } |> Seq.iter (fun (row, ctx) -> formatTableRow ctx table row)

/// Write a list of MarkdownParagraph values to a PDF document
and formatParagraphs (ctx : Context) addParagraph = List.iter (formatParagraph ctx addParagraph)

/// Write a MarkdownTableRow value to a PDF document
and formatTableRow (ctx : Context) (table : Table) (mdRow : MarkdownTableRow) = 
    let row = table.AddRow()
    mdRow |> List.iteri (fun idx paragraphs -> 
        let cell = row.Cells.[idx]
        formatParagraphs ctx cell.AddParagraph paragraphs)

/// Format Markdown document and write the result to the specified PDF document
let formatMarkdown (document : Document) links paragraphs = 
    setDefaultStyles document
    document.AddSection() |> ignore

    let ctx = { 
                Document        = document
                Links           = links
                StyleOverride   = None
                BoldOverride    = None
                ItalicOverride  = None 
              }
    
    formatParagraphs ctx document.LastSection.AddParagraph paragraphs

    let renderer = PdfDocumentRenderer(false, PdfFontEmbedding.Always)
    renderer.Document <- document
    renderer.RenderDocument()
    renderer.PdfDocument