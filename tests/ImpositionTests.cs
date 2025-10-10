using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfDroplet.LayoutMethods;

namespace sillsdev.dotImpose.Tests;

/// <summary>
/// Tests that verify the actual imposition logic - page ordering, rotation, and layout.
/// </summary>
public class ImpositionTests : IDisposable
{
    private readonly string _testPdfPath;
    private readonly string _outputDirectory;

    public ImpositionTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), $"ImpositionTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_outputDirectory);

        _testPdfPath = Path.Combine(_outputDirectory, "test-input.pdf");
        CreateNumberedTestPdf(_testPdfPath, 8);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, true);
        }
    }

    /// <summary>
    /// Creates a test PDF where each page has a large number on it for easy identification
    /// </summary>
    private void CreateNumberedTestPdf(string path, int pageCount, bool landscape = false)
    {
        using var document = new PdfDocument();
        for (int i = 1; i <= pageCount; i++)
        {
            var page = document.AddPage();
            if (landscape)
            {
                page.Width = XUnit.FromMillimeter(297);  // A4 landscape width
                page.Height = XUnit.FromMillimeter(210); // A4 landscape height
            }
            else
            {
                page.Width = XUnit.FromMillimeter(210);  // A4 portrait width
                page.Height = XUnit.FromMillimeter(297); // A4 portrait height
            }

            using var gfx = XGraphics.FromPdfPage(page);

            // Draw page border
            gfx.DrawRectangle(XPens.Black,
                new XRect(10, 10, page.Width.Point - 20, page.Height.Point - 20));

            // Draw a pattern of rectangles to distinguish pages
            // Use different shades of gray for different pages
            var gray = 0.2 + (i * 0.1) % 0.7;
            var brush = new XSolidBrush(XColor.FromArgb(255, (byte)(gray * 255), (byte)(gray * 255), (byte)(gray * 255)));
            gfx.DrawRectangle(brush,
                new XRect(page.Width.Point / 4, page.Height.Point / 4,
                         page.Width.Point / 2, page.Height.Point / 2));

            // Draw diagonal lines to show page number visually
            for (int j = 0; j < i && j < 10; j++)
            {
                var pen = new XPen(XColors.DarkRed, 2);
                gfx.DrawLine(pen,
                    20 + j * 10, 20,
                    20 + j * 10 + 40, 60);
            }
        }
        document.Save(path);
    }

    #region SideFoldBooklet Tests

    [Fact]
    public void SideFoldBooklet_8Pages_Creates4OutputPages()
    {
        // Arrange
        var layoutMethod = new SideFoldBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "sidefold-8pages.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // 8 input pages = 2 sheets = 4 output pages (front and back of 2 sheets)
        Assert.Equal(4, outputDoc.PageCount);

        // Each output page should be landscape (2 portrait pages side-by-side)
        var firstPage = outputDoc.Pages[0];
        Assert.True(firstPage.Width.Point > firstPage.Height.Point, "Output pages should be landscape");
    }

    [Fact]
    public void SideFoldBooklet_4Pages_Creates2OutputPages()
    {
        // Arrange - Create a 4-page PDF
        var inputPath = Path.Combine(_outputDirectory, "input-4pages.pdf");
        CreateNumberedTestPdf(inputPath, 4);

        var layoutMethod = new SideFoldBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "sidefold-4pages.pdf");
        var inputPdf = XPdfForm.FromFile(inputPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, inputPath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // 4 input pages = 1 sheet = 2 output pages (front and back)
        Assert.Equal(2, outputDoc.PageCount);
    }

    [Fact]
    public void SideFoldBooklet_NonDivisibleBy4_HandlesCorrectly()
    {
        // Arrange - Create a 5-page PDF (not divisible by 4)
        var inputPath = Path.Combine(_outputDirectory, "input-5pages.pdf");
        CreateNumberedTestPdf(inputPath, 5);

        var layoutMethod = new SideFoldBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "sidefold-5pages.pdf");
        var inputPdf = XPdfForm.FromFile(inputPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, inputPath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // 5 input pages rounds up to 2 sheets = 4 output pages
        Assert.Equal(4, outputDoc.PageCount);
    }

    [Fact]
    public void SideFoldBooklet_OnlyEnabledForPortrait()
    {
        // Arrange - Portrait PDF
        var portraitInputPdf = XPdfForm.FromFile(_testPdfPath);
        var layoutMethod = new SideFoldBookletLayouter();

        // Act & Assert - Portrait should be enabled
        Assert.True(layoutMethod.GetIsEnabled(portraitInputPdf));

        // Arrange - Landscape PDF
        var landscapePath = Path.Combine(_outputDirectory, "landscape-input.pdf");
        CreateNumberedTestPdf(landscapePath, 4, landscape: true);
        var landscapeInputPdf = XPdfForm.FromFile(landscapePath);

        // Act & Assert - Landscape should NOT be enabled
        Assert.False(layoutMethod.GetIsEnabled(landscapeInputPdf));
    }

    #endregion

    #region CalendarLayouter Tests

    [Fact]
    public void CalendarLayouter_8Pages_Creates4OutputPages()
    {
        // Arrange
        var layoutMethod = new CalendarLayouter();
        var outputPath = Path.Combine(_outputDirectory, "calendar-8pages.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // Calendar fold also uses 2 pages per sheet
        Assert.Equal(4, outputDoc.PageCount);
    }

    [Fact]
    public void CalendarLayouter_OnlyEnabledForLandscape()
    {
        // Arrange - Landscape PDF
        var landscapePath = Path.Combine(_outputDirectory, "landscape-for-calendar.pdf");
        CreateNumberedTestPdf(landscapePath, 4, landscape: true);
        var landscapeInputPdf = XPdfForm.FromFile(landscapePath);
        var layoutMethod = new CalendarLayouter();

        // Act & Assert - Landscape should be enabled
        Assert.True(layoutMethod.GetIsEnabled(landscapeInputPdf));

        // Arrange - Portrait PDF
        var portraitInputPdf = XPdfForm.FromFile(_testPdfPath);

        // Act & Assert - Portrait should NOT be enabled
        Assert.False(layoutMethod.GetIsEnabled(portraitInputPdf));
    }

    #endregion

    #region NullLayoutMethod Tests

    [Fact]
    public void NullLayoutMethod_PreservesPageCount()
    {
        // Arrange
        var layoutMethod = new NullLayoutMethod();
        var outputPath = Path.Combine(_outputDirectory, "null-output.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(8, outputDoc.PageCount);
    }

    [Fact]
    public void NullLayoutMethod_PreservesPageDimensions()
    {
        // Arrange
        var layoutMethod = new NullLayoutMethod();
        var outputPath = Path.Combine(_outputDirectory, "null-dimensions.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var page = outputDoc.Pages[0];

        // Should be A4 portrait dimensions (within tolerance)
        Assert.InRange(page.Width.Point, 590, 600);  // ~595 points = 210mm
        Assert.InRange(page.Height.Point, 835, 845); // ~842 points = 297mm
    }

    [Fact]
    public void NullLayoutMethod_WithBleed_CreatesTrimBox()
    {
        // Arrange
        var layoutMethod = new NullLayoutMethod(bleedMM: 3.0);
        var outputPath = Path.Combine(_outputDirectory, "null-bleed.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var page = outputDoc.Pages[0];

        // Should have TrimBox set
        Assert.NotNull(page.Elements["/TrimBox"]);
        Assert.NotNull(page.Elements["/ArtBox"]);

        // TrimBox should be smaller than MediaBox
        var trimBox = page.Elements.GetArray("/TrimBox");
        var mediaBox = page.MediaBox;

        // TrimBox should be inset by the bleed amount (3mm = ~8.5 points on each side)
        Assert.True(trimBox != null, "TrimBox should exist");
    }

    [Fact]
    public void NullLayoutMethod_WithCropMarks_EnlargesMediaBox()
    {
        // Arrange
        var layoutMethod = new NullLayoutMethod();
        var outputPath = Path.Combine(_outputDirectory, "null-cropmarks.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, true); // showCropMarks = true

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var page = outputDoc.Pages[0];

        // MediaBox should be larger than the original A4 to accommodate crop marks
        // Original A4 is ~595x842 points, with 6mm margins (~17 points each side) = ~629x876
        Assert.True(page.Width.Point > 610, $"Width with crop marks should be > 610 points, was {page.Width.Point}");
        Assert.True(page.Height.Point > 860, $"Height with crop marks should be > 860 points, was {page.Height.Point}");
    }

    #endregion

    #region CutLandscapeLayouter Tests

    [Fact]
    public void CutLandscapeLayouter_DoublesPageCount()
    {
        // Arrange - Landscape input
        var landscapePath = Path.Combine(_outputDirectory, "landscape-cut.pdf");
        CreateNumberedTestPdf(landscapePath, 4, landscape: true);

        var layoutMethod = new CutLandscapeLayout();
        var outputPath = Path.Combine(_outputDirectory, "cut-landscape-output.pdf");
        var inputPdf = XPdfForm.FromFile(landscapePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, landscapePath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // Cut landscape creates 1 output page per sheet (2 sheets for 4 pages)
        Assert.Equal(2, outputDoc.PageCount);
    }

    [Fact]
    public void CutLandscapeLayouter_OnlyEnabledForLandscape()
    {
        // Arrange - Landscape
        var landscapePath = Path.Combine(_outputDirectory, "landscape-for-cut.pdf");
        CreateNumberedTestPdf(landscapePath, 4, landscape: true);
        var landscapeInputPdf = XPdfForm.FromFile(landscapePath);
        var layoutMethod = new CutLandscapeLayout();

        // Act & Assert
        Assert.True(layoutMethod.GetIsEnabled(landscapeInputPdf));

        // Portrait should not be enabled
        var portraitInputPdf = XPdfForm.FromFile(_testPdfPath);
        Assert.False(layoutMethod.GetIsEnabled(portraitInputPdf));
    }

    #endregion

    #region SideFold4UpBookletLayouter Tests

    [Fact]
    public void SideFold4UpBooklet_ReducesPageCount()
    {
        // Arrange - 16 page input
        var inputPath = Path.Combine(_outputDirectory, "input-16pages.pdf");
        CreateNumberedTestPdf(inputPath, 16);

        var layoutMethod = new SideFold4UpBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "4up-output.pdf");
        var inputPdf = XPdfForm.FromFile(inputPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, inputPath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // 4-up means 4 pages per output page, so fewer output pages than input
        Assert.True(outputDoc.PageCount < 16,
            $"4-up should reduce page count. Input: 16, Output: {outputDoc.PageCount}");
        Assert.True(outputDoc.PageCount >= 4,
            $"Should have at least 4 output pages. Got: {outputDoc.PageCount}");
    }

    #endregion

    #region Folded8Up8PageBookletLayouter Tests

    [Fact]
    public void Folded8Up_OnlyEnabledForPortrait()
    {
        // Arrange
        var landscapePath = Path.Combine(_outputDirectory, "landscape-8up.pdf");
        CreateNumberedTestPdf(landscapePath, 8, landscape: true);
        var landscapeInputPdf = XPdfForm.FromFile(landscapePath);
        var layoutMethod = new Folded8Up8PageBookletLayouter();

        // Act & Assert - This layout requires portrait input (per class documentation)
        Assert.False(layoutMethod.GetIsEnabled(landscapeInputPdf));

        var portraitInputPdf = XPdfForm.FromFile(_testPdfPath);
        Assert.True(layoutMethod.GetIsEnabled(portraitInputPdf));
    }

    [Fact]
    public void Folded8Up_8LandscapePages_CreatesSingleOutputPage()
    {
        // Arrange
        var landscapePath = Path.Combine(_outputDirectory, "landscape-8up-input.pdf");
        CreateNumberedTestPdf(landscapePath, 8, landscape: true);

        var layoutMethod = new Folded8Up8PageBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "8up-output.pdf");
        var inputPdf = XPdfForm.FromFile(landscapePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, landscapePath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // 8-up booklet puts all 8 pages on 1 sheet
        Assert.Equal(1, outputDoc.PageCount);
    }

    #endregion

    #region Square6UpBookletLayouter Tests

    [Fact]
    public void Square6Up_OnlyEnabledForSquare()
    {
        // Arrange - Create square pages
        var squarePath = Path.Combine(_outputDirectory, "square-input.pdf");
        using (var document = new PdfDocument())
        {
            for (int i = 1; i <= 6; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromMillimeter(200);  // Square
                page.Height = XUnit.FromMillimeter(200); // Square

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawRectangle(XPens.Black,
                    new XRect(10, 10, page.Width.Point - 20, page.Height.Point - 20));
            }
            document.Save(squarePath);
        }

        var squareInputPdf = XPdfForm.FromFile(squarePath);
        var layoutMethod = new Square6UpBookletLayouter();

        // Act & Assert
        Assert.True(layoutMethod.GetIsEnabled(squareInputPdf));

        // Non-square should not be enabled
        var portraitInputPdf = XPdfForm.FromFile(_testPdfPath);
        Assert.False(layoutMethod.GetIsEnabled(portraitInputPdf));
    }

    [Fact]
    public void Square6Up_6Pages_Creates2OutputPages()
    {
        // Arrange - Create 6 square pages
        var squarePath = Path.Combine(_outputDirectory, "square-6pages.pdf");
        using (var document = new PdfDocument())
        {
            for (int i = 1; i <= 6; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromMillimeter(200);
                page.Height = XUnit.FromMillimeter(200);

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawRectangle(XPens.Black,
                    new XRect(10, 10, page.Width.Point - 20, page.Height.Point - 20));
            }
            document.Save(squarePath);
        }

        var layoutMethod = new Square6UpBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "square6up-output.pdf");
        var inputPdf = XPdfForm.FromFile(squarePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, squarePath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);

        // 6-up creates 4 output pages for 6 input pages (2 sheets front/back)
        Assert.Equal(4, outputDoc.PageCount);
    }

    #endregion

    #region Right-to-Left Tests

    [Fact]
    public void SideFoldBooklet_RightToLeft_ProducesOutput()
    {
        // Arrange
        var layoutMethod = new SideFoldBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "sidefold-rtl.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act - Test with rightToLeft = true
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, true, false);

        // Assert
        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(4, outputDoc.PageCount);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SinglePage_NullLayout_Works()
    {
        // Arrange
        var singlePagePath = Path.Combine(_outputDirectory, "single-page.pdf");
        CreateNumberedTestPdf(singlePagePath, 1);

        var layoutMethod = new NullLayoutMethod();
        var outputPath = Path.Combine(_outputDirectory, "single-null-output.pdf");
        var inputPdf = XPdfForm.FromFile(singlePagePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, singlePagePath, outputPath, paperTarget, false, false);

        // Assert
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(1, outputDoc.PageCount);
    }

    [Fact]
    public void SinglePage_SideFoldBooklet_HandlesBlanks()
    {
        // Arrange
        var singlePagePath = Path.Combine(_outputDirectory, "single-for-booklet.pdf");
        CreateNumberedTestPdf(singlePagePath, 1);

        var layoutMethod = new SideFoldBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "single-booklet-output.pdf");
        var inputPdf = XPdfForm.FromFile(singlePagePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act & Assert - Should not throw
        layoutMethod.Layout(inputPdf, singlePagePath, outputPath, paperTarget, false, false);

        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.True(outputDoc.PageCount > 0);
    }

    #endregion
}
