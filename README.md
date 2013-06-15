# FSharp.Markdown.Pdf


PDF formatting from FSharp.Markdown (part of the [F# Formatting](https://github.com/tpetricek/FSharp.Formatting) project).

### Nuget

<a href="https://nuget.org/packages/FSharp.Markdown.Pdf/"><img src="http://theburningmonk.com/images/Markdown-PDF-nuget-install.png" alt="NuGet package"/></a>


### F# Examples

From F#, you can use the `Markdown.TransformPdf` and `Markdown.WritePdf` extension methods:

    open FSharp.Markdown
    open FSharp.Markdown.Pdf

    let text = """Some text in Mardown format..."""
    
    // formatting to PDF and save result to file
    Markdown.TransformPdf(text, @"C:\temp\markdown.pdf")
    
    // formatting to PDF and save result to stream
    use stream = File.OpenWrite(@"C:\temp\markdown2.pdf")
    Markdown.TransformPdf(text, stream)
    
    let parsed = Markdown.Parse(text)
    
    // taking a tokenized Markdown document and formatting to PDF
    // before saving result to file
    Markdown.WritePdf(parsed, @"C:\temp\markdown3.pdf")
    
    // taking a tokenized Markdown document and formatting to PDF
    // before saving result to stream
    use stream' = File.OpenWrite(@"C:\temp\markdown4.pdf")
    Markdown.WritePdf(parsed, stream')
    
### C# Examples

The `Markdown.TransformPdf` and `Markdown.WritePdf` extensions methods are not visible to C# due to the way F# extension methods defined with the `type XYZ with` syntax are compiled, so in its place, there is a `MarkdownPdf` type which you can use from C#:
    
    using FSharp.Markdown;
    using FSharp.Markdown.Pdf;
    
    var text = "Some text in Markdown format...";
    
    // formatting to PDF and save result to file
    MarkdownPdf.Transform(text, @"C:\temp\markdown.pdf");
    
    // formatting to PDF and save result to stream
    using (var stream = File.OpenWrite(@"C:\temp\markdown2.pdf"))
    {
        MarkdownPdf.Transform(text, stream);
    }
    
    var parsed = Markdown.Parse(text);
    
    // taking a tokenized Markdown document and formatting to PDF
    // before saving result to file
    MarkdownPdf.Write(parsed, @"C:\temp\markdown3.pdf");
    
    // taking a tokenized Markdown document and formatting to PDF
    // before saving result to stream
    using (var stream2 = File.OpenWrite(@"C:\temp\markdown4.pdf"))
    {
        MarkdownPdf.Write(parsed, stream2);
    }
