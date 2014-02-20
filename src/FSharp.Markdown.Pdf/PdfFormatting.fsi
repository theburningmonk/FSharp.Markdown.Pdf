﻿namespace FSharp.Markdown.Pdf

open System.Collections.Generic
open System.IO

open FSharp.Markdown
open MigraDoc.DocumentObjectModel
open PdfSharp.Pdf

// Type alias
type LinkId    = string
type Link      = string
type Title     = string option

[<AutoOpen>]
module PdfFormatting =
    /// function to format a set of reference links and the tokenized Markdown document into
    /// a PdfDocument value using the Document value specified (which can contain style 
    /// overrides for all the styles listed in MarkdownStyleNames).
    val formatMarkdown : Document -> IDictionary<LinkId, Link * Title> -> MarkdownParagraphs -> PdfDocument

[<AutoOpen>]
module MarkdownExt =
    /// Extension methods for the Markdown type defined in FSharp.Markdown namespace
    type Markdown with
        /// Transform Markdown document into PDF. The result will be written to the 
        /// provided output path.
        static member TransformPdf  : string * string -> unit

        /// Transform Markdown document into PDF. The result will be written to the
        /// provided stream.
        static member TransformPdf  : string * Stream -> unit

        /// Transform the provided Markdown document into PDF. The result will be 
        /// written to the provided output path.
        static member WritePdf      : MarkdownDocument * string -> unit

        /// Transform the provided Markdown document into PDF. The result will be 
        /// written to the provided stream.
        static member WritePdf      : MarkdownDocument * Stream -> unit

        /// Transform the provided Markdown document into PDF. The result will be written
        /// to the provided document, added to the provided section.
        static member AddMarkdown   : Document *  Section * string -> unit

        /// Transform the provided Markdown document into PDF. The result will be written
        /// to the provided document, added to the provided section.
        static member AddMarkdown   : Document * Section * MarkdownDocument -> unit

/// Type akin to Markdown to expose the above static extension methods so that they're visible in C#
/// since the extension methods defined with 'type XXX with' in F# are not visible in C#
[<Class>]
type MarkdownPdf =
    /// Transform Markdown document into PDF. The result will be written to the 
    /// provided output path.
    static member Transform : string * string -> unit

    /// Transform Markdown document into PDF. The result will be written to the
    /// provided stream.
    static member Transform : string * Stream -> unit

    /// Transform the provided Markdown document into PDF. The result will be 
    /// written to the provided output path.
    static member Write     : MarkdownDocument * string -> unit

    /// Transform the provided Markdown document into PDF. The result will be 
    /// written to the provided stream.
    static member Write     : MarkdownDocument * Stream -> unit

    /// Transform the provided Markdown document into PDF. The result will be written
    /// to the provided document, added to the provided section.
    static member AddMarkdown : Document * Section * string -> unit

    /// Transform the provided Markdown document into PDF. The result will be written
    /// to the provided document, added to the provided section.
    static member AddMarkdown : Document * Section * MarkdownDocument -> unit

