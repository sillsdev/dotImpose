# DotImpose Library

A .NET library for PDF imposition and layout operations, supporting various booklet and calendar layouts.

[PdfDroplet](https://github.com/sillsdev/PdfDroplet) and [Bloom](https://github.com/BloomBooks/BloomDesktop) use this library for generating print-ready PDF layouts.

## Installation

Get it from nuget:

```bash
dotnet add package SILLsDev.DotImpose
```

## Usage

```csharp
using sillsdev.dotImpose.LayoutMethods;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

// Open an input PDF
var inputPdf = XPdfForm.FromFile("input.pdf");

// Choose a layout method
var layoutMethod = new SideFoldBookletLayouter(); // or other layout methods

// Access layout method information
Console.WriteLine($"Layout ID: {layoutMethod.Id}");                 // "sideFoldBooklet"
Console.WriteLine($"Layout Label: {layoutMethod.EnglishLabel}");    // "Fold Booklet"

// Define paper target
var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

// Perform the layout
layoutMethod.Layout(inputPdf, "input.pdf", "output.pdf", paperTarget, rightToLeft: false, showCropMarks: false);
```

## Available Layout Methods

Each layout method has an `Id` property (for programmatic use) and an `EnglishLabel` property (for display):

| Class                              | Id                            | English Label               | Description                  |
| ---------------------------------- | ----------------------------- | --------------------------- | ---------------------------- |
| `NullLayoutMethod`                 | `original`                    | Original                    | Original layout (no changes) |
| `SideFoldBookletLayouter`          | `sideFoldBooklet`             | Fold Booklet                | Side-fold booklet layout     |
| `CalendarLayouter`                 | `calendar`                    | Calendar Fold               | Calendar fold layout         |
| `CutLandscapeLayout`               | `cutBooklet`                  | Cut & Stack                 | Cut landscape layout         |
| `SideFold4UpBookletLayouter`       | `sideFoldCut4UpBooklet`       | Fold/Cut 4Up Booklet        | 4-up side-fold booklet       |
| `SideFold4UpSingleBookletLayouter` | `sideFoldCut4UpSingleBooklet` | Fold/Cut Single 4Up Booklet | 4-up single booklet          |
| `Folded8Up8PageBookletLayouter`    | `folded8Up8PageBooklet`       | Fold/Cut 8Up 8 Page Booklet | 8-up folded booklet          |
| `Square6UpBookletLayouter`         | `square6UpBooklet`            | Fold/Cut 6Up Square Booklet | 6-up square booklet          |

## Features

- Multiple PDF imposition layouts
- Support for right-to-left languages
- Crop marks for commercial printing

## Building

Build the solution using .NET 8.0:

```bash
dotnet build
dotnet pack --configuration Release
```

## License

This project is licensed under the MIT License.

Copyright © SIL Global 2012-2025
