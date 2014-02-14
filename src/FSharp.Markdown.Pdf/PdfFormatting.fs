namespace FSharp.Markdown.Pdf

open System
open System.Collections.Generic
open System.Linq
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
type ListDepth = int
type ListIndex = int
type ListState = MarkdownListKind * ListDepth * ListIndex

/// Context passed around while formatting the PDF document
type Context = 
    { 
        Document            : Document
        Links               : IDictionary<LinkId, Link * Title>
        StyleOverride       : StyleName option
        BoldOverride        : bool option
        ItalicOverride      : bool option
        ListState           : ListState option
    }

[<AutoOpen>]
module PdfFormatting =
    /// Sets the default styles if user-provided overrides do not exist
    let setDefaultStyles (document : Document) =
        let setIfNotExist styleName baseStyleName update =
            // document.Styles.HasValue doesn't work as expected, hence the weirdness here..
            let exists = document.Styles.Cast<Style>() |> Seq.exists (fun style -> style.Name = styleName)
            if not exists then
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

        setIfNotExist MarkdownStyleNames.List1 MarkdownStyleNames.Normal 
                      (fun style -> style.ParagraphFormat.LeftIndent        <- Unit.FromPoint 7.0)
        setIfNotExist MarkdownStyleNames.List2 MarkdownStyleNames.Normal 
                      (fun style -> style.ParagraphFormat.LeftIndent        <- Unit.FromPoint 14.0)
        setIfNotExist MarkdownStyleNames.List3 MarkdownStyleNames.Normal 
                      (fun style -> style.ParagraphFormat.LeftIndent        <- Unit.FromPoint 21.0)

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

    /// Returns the ListType value based on the MarkdownListType and a zero-based depth
    let getListType = 
        let orderedListTypes   = [| ListType.NumberList1; ListType.NumberList2; ListType.NumberList3 |]
        let unorderedListTypes = [| ListType.BulletList1; ListType.BulletList2; ListType.BulletList3 |]

        (fun kind depth -> 
            match kind with | Ordered -> orderedListTypes.[depth] | _ -> unorderedListTypes.[depth])

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

    /// Creates a new paragraph using the specified context and the factory function
    let getParagraph (ctx : Context) (make : unit -> Paragraph) =
        let pdfParagraph = make()
        match ctx.StyleOverride with | Some styleName -> pdfParagraph.Style <- styleName | _ -> ()
        match ctx.ListState with 
        | Some (kind, depth, idx) -> 
            let listInfo = ListInfo()
            listInfo.ContinuePreviousList <- idx > 0
            listInfo.ListType <- getListType kind depth
            pdfParagraph.Format.ListInfo <- listInfo
        | _ -> ()

        pdfParagraph

    /// Write a MarkdownParagraph value to a PDF document
    let rec formatParagraph (ctx : Context) (make : unit -> Paragraph) = function
        | Heading(n, spans) -> 
            let paragraph = getParagraph ctx make
            paragraph.Style <- "MdHeading" + string n
            formatSpans ctx paragraph spans

        | Paragraph(spans)       
        | Span(spans)       ->
            let paragraph = getParagraph ctx make
            formatSpans ctx paragraph spans
    
        // treat the raw HTML as 'code', without writing a HTML to PDF renderer ;-)
        | HtmlBlock code
        | CodeBlock(code)   -> 
            let paragraph = getParagraph ctx make
            paragraph.Style <- MarkdownStyleNames.Code
            paragraph |> addFormattedText ctx code |> ignore

        | ListBlock(kind, items) ->        
            let listDepth = match ctx.ListState with | Some (_, depth, _) -> depth + 1 | _ -> 0
            let ctx = { ctx with StyleOverride = Some <| "MdList" + string (listDepth + 1) }

            items 
            |> List.iteri (fun idx paragraphs ->
                let ctx = { ctx with ListState = Some (kind, listDepth, idx) }
                formatParagraphs ctx make paragraphs)

        | QuotedBlock paragraphs -> 
            let ctx = { ctx with StyleOverride = Some MarkdownStyleNames.Quoted }
            formatParagraphs ctx make paragraphs

        // not sure if it's the right choice, but let's interpret horizontal rule as a page break
        | HorizontalRule         -> 
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

        | _ -> ()

    /// Write a list of MarkdownParagraph values to a PDF document
    and formatParagraphs (ctx : Context) make = List.iter (formatParagraph ctx make)

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
                    Document            = document
                    Links               = links
                    StyleOverride       = None
                    BoldOverride        = None
                    ItalicOverride      = None
                    ListState           = None
                  }
    
        formatParagraphs ctx document.LastSection.AddParagraph paragraphs

        let renderer = PdfDocumentRenderer(false, PdfFontEmbedding.Always)
        renderer.Document <- document
        renderer.RenderDocument()
        renderer.PdfDocument

    /// Format Markdown document and write the result to the specified section
    let addMarkdown (document : Document) (section : Section) links paragraphs =
        setDefaultStyles document

        let ctx = { 
                    Document            = document
                    Links               = links
                    StyleOverride       = None
                    BoldOverride        = None
                    ItalicOverride      = None
                    ListState           = None
                  }
    
//        formatParagraphs ctx document.LastSection.AddParagraph paragraphs
        formatParagraphs ctx section.AddParagraph paragraphs

[<AutoOpen>]
module MarkdownExt =
    type Markdown with
        static member TransformPdf(text, outputPath : string) =
            let doc = Markdown.Parse(text)
            Markdown.WritePdf(doc, outputPath)

        static member TransformPdf(text, stream : Stream) =
            let doc = Markdown.Parse(text)
            Markdown.WritePdf(doc, stream)

        static member TransformPdf(text) =
            let doc = Markdown.Parse(text)
            Markdown.WritePdf(doc)

        static member WritePdf(doc : MarkdownDocument, outputPath : string) =
            let pdfDocument = formatMarkdown (new Document()) doc.DefinedLinks doc.Paragraphs
            pdfDocument.Save(outputPath)

        static member WritePdf(doc : MarkdownDocument) =
            let pdfDocument = formatMarkdown (new Document()) doc.DefinedLinks doc.Paragraphs
            let stream = new MemoryStream()
            pdfDocument.Save(stream)
            stream :> Stream

        static member WritePdf(doc : MarkdownDocument, stream : Stream) =
            let pdfDocument = formatMarkdown (new Document()) doc.DefinedLinks doc.Paragraphs
            pdfDocument.Save(stream)

        static member AddMarkdown (document : Document, section : Section, text) = 
            let doc = Markdown.Parse(text)
            addMarkdown document section doc.DefinedLinks doc.Paragraphs

        static member AddMarkdown (document : Document, section : Section, mdDoc : MarkdownDocument) = 
            addMarkdown document section mdDoc.DefinedLinks mdDoc.Paragraphs

type MarkdownPdf =
    static member Transform(text, outputPath : string) = Markdown.TransformPdf(text, outputPath)
    static member Transform(text, stream : Stream)     = Markdown.TransformPdf(text, stream)
    static member Write(doc, outputPath : string)      = Markdown.WritePdf(doc, outputPath)
    static member Write(doc, stream : Stream)          = Markdown.WritePdf(doc, stream)
    static member AddMarkdown(doc, section, text : string)      = Markdown.AddMarkdown(doc, section, text)
    static member AddMarkdown(doc, section, mdDoc : MarkdownDocument) = Markdown.AddMarkdown(doc, section, mdDoc)
