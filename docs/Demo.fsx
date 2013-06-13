#I "../bin"
#r "FSharp.Markdown.dll"
#r "PdfSharp.dll"
#r "MigraDoc.DocumentObjectModel.dll"
#r "FSharp.Markdown.Pdf.dll"

open System.IO

open FSharp.Markdown
open FSharp.Markdown.Pdf

open MigraDoc.DocumentObjectModel

let source = __SOURCE_DIRECTORY__
let outputPath = Path.Combine(source, "output")
let testFilesPath = Path.Combine(source, "testfiles")

let testFiles = Directory.GetFiles(testFilesPath) |> Array.filter (fun path -> path.EndsWith ".text")
let outputFiles = 
    for testFile in testFiles do
        let filename = Path.GetFileNameWithoutExtension testFile
        let markdownDoc = File.ReadAllText testFile
        let parsed = Markdown.Parse(markdownDoc)
        let pdfDoc = formatMarkdown (new Document()) parsed.DefinedLinks parsed.Paragraphs
        
        let outputFile = Path.Combine(outputPath, filename + ".pdf")
        pdfDoc.Save(outputFile)