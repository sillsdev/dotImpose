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
- Full-bleed aware imposition, crop marks.

## Full Bleed

DotImpose supports full-bleed workflows where the client application provides source PDFs with the expected PDF page boxes already defined (`TrimBox`, `BleedBox`, etc.).

### NullLayoutMethod

- Preserves source page content and source box geometry as-is (see note about Crop Marks below).
- `NullLayoutMethod(insetTrimboxMillimeters)` is deprecated.
- If a source page defines an explicit `TrimBox`, using non-zero `insetTrimboxMillimeters` now throws an error.
- If a source page does not define an explicit `TrimBox`, non-zero `insetTrimboxMillimeters` synthesizes a `TrimBox` by insetting from source bleed/media geometry. In other words, if you give it an A5 page and specify an insetTrimboxMillimeters value, you will get back pages where the TrimBox is smaller than A5.
- Preferred approach: use `NullLayoutMethod()` and provide explicit source `TrimBox`/`BleedBox` in your input PDF.

### SideFoldBookletLayouter

- Uses source `TrimBox` as the panel trim intent for each imposed page panel.
- Preserves source bleed/trim intent so cutoff matches source expectations.
- Does not impose a special fold-edge clipping rule on your behalf.
- If you want content to print all the way to the fold, define source boxes accordingly (for example by setting trim/bleed so that edge is treated as printable).
- If you want content to stop at the fold, define source boxes so that edge trims there.

The output page boxes describe the final imposed sheet, while source box definitions remain the authority for panel-level cutoff intent.

### Crop Marks

When crop marks are enabled, DotImpose adds marks outside the final trim area. Crop marks do not change trim or bleed calculations; if needed, DotImpose will expand the `MediaBox` to make room for them.

## Building

Build the solution using .NET 8.0:

```bash
dotnet build
dotnet pack --configuration Release
```

## License

This project is licensed under the MIT License.

Copyright © SIL Global 2012-2025
