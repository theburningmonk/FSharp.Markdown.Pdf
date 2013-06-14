using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSharp.Markdown;
using FSharp.Markdown.Pdf;

namespace FSharp.Markdown.Pdf.TestsCs
{
    class Program
    {
        static void Main(string[] args)
        {
            var markdown = @"
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
";

            MarkdownPdf.Transform(markdown, @"C:\temp\markdown.pdf");
            Process.Start(@"C:\temp\markdown.pdf");

            using (var fileStream = File.OpenWrite(@"C:\temp\markdown2.pdf"))
            {
                MarkdownPdf.Transform(markdown, fileStream);
                Process.Start(@"C:\temp\markdown2.pdf");
            }

            var parsed = Markdown.Parse(markdown);

            MarkdownPdf.Write(parsed, @"C:\temp\markdown3.pdf");
            Process.Start(@"C:\temp\markdown3.pdf");

            using (var fileStream = File.OpenWrite(@"C:\temp\markdown4.pdf"))
            {
                MarkdownPdf.Write(parsed, fileStream);
                Process.Start(@"C:\temp\markdown4.pdf");
            }

            Console.WriteLine("All done... press any key to exit");
            Console.ReadKey();
        }
    }
}
