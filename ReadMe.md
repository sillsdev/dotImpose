# DotImpose Library

A .NET library for PDF imposition and layout operations, supporting various booklet and calendar layouts.

[PdfDroplet](https://github.com/sillsdev/pdfdroplet) and [Bloom](https://github.com/BloomBooks/BloomDesktop) use this library for generating print-ready PDF layouts.

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

// Define paper target
var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

// Perform the layout
layoutMethod.Layout(inputPdf, "input.pdf", "output.pdf", paperTarget, rightToLeft: false, showCropMarks: false);
```

## Available Layout Methods

- `NullLayoutMethod` - Original layout (no changes)
- `SideFoldBookletLayouter` - Side-fold booklet layout
- `CalendarLayouter` - Calendar fold layout
- `CutLandscapeLayout` - Cut landscape layout
- `SideFold4UpBookletLayouter` - 4-up side-fold booklet
- `SideFold4UpSingleBookletLayouter` - 4-up single booklet
- `Folded8Up8PageBookletLayouter` - 8-up folded booklet
- `Square6UpBookletLayouter` - 6-up square booklet

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
