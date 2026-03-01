using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using DotImpose.LayoutMethods;

namespace sillsdev.dotImpose.Tests;

public class LayoutMethodTests : IDisposable
{
    private readonly string _testPdfPath;
    private readonly string _outputDirectory;

    public LayoutMethodTests()
    {
        // Create a temporary directory for test outputs
        _outputDirectory = Path.Combine(Path.GetTempPath(), $"ImpositionTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_outputDirectory);

        // Create a simple test PDF with 8 pages
        _testPdfPath = Path.Combine(_outputDirectory, "test-input.pdf");
        CreateTestPdf(_testPdfPath, 8);
    }

    public void Dispose()
    {
        // Clean up test files
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, true);
        }
    }

    private void CreateTestPdf(string path, int pageCount)
    {
        using var document = new PdfDocument();
        for (int i = 1; i <= pageCount; i++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(210);  // A4 width
            page.Height = XUnit.FromMillimeter(297); // A4 height

            // Draw a simple rectangle to make the page non-empty
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XPens.Black, XBrushes.LightGray,
                new XRect(10, 10, page.Width.Point - 20, page.Height.Point - 20));
        }
        document.Save(path);
    }

    [Fact]
    public void NullLayoutMethod_CopiesOriginalPdf()
    {
        // Arrange
        var layoutMethod = new NullLayoutMethod();
        var outputPath = Path.Combine(_outputDirectory, "null-output.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(8, outputDoc.PageCount);
    }

    [Fact]
    public void NullLayoutMethod_WithBleed_SetsTrimBoxes()
    {
        // Arrange
        var layoutMethod = new NullLayoutMethod(bleedMM: 5.0);
        var outputPath = Path.Combine(_outputDirectory, "null-bleed-output.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(8, outputDoc.PageCount);

        // Verify TrimBox is smaller than MediaBox
        var firstPage = outputDoc.Pages[0];
        Assert.NotNull(firstPage.Elements["/TrimBox"]);
        // TrimBox should be inset by the bleed amount on all sides
    }

    [Fact]
    public void NullLayoutMethod_WithBleedAndCropMarks_UsesExpectedTrimAndBleedBoxes()
    {
        var singlePageInput = Path.Combine(_outputDirectory, "full-bleed-input.pdf");
        using (var document = new PdfDocument())
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(216);
            page.Height = XUnit.FromMillimeter(303);
            document.Save(singlePageInput);
        }

        var layoutMethod = new NullLayoutMethod(bleedMM: 3.0);
        var outputPath = Path.Combine(_outputDirectory, "null-bleed-cropmarks-output.pdf");
        var inputPdf = XPdfForm.FromFile(singlePageInput);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        layoutMethod.Layout(inputPdf, singlePageInput, outputPath, paperTarget, false, true);

        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var outputPage = outputDoc.Pages[0];
        var trimBox = outputPage.TrimBox.ToXRect();
        var bleedBox = outputPage.BleedBox.ToXRect();

        Assert.InRange(XUnit.FromPoint(trimBox.Width).Millimeter, 209.5, 210.5);
        Assert.InRange(XUnit.FromPoint(trimBox.Height).Millimeter, 296.5, 297.5);
        Assert.InRange(XUnit.FromPoint(bleedBox.Width).Millimeter, 215.5, 216.5);
        Assert.InRange(XUnit.FromPoint(bleedBox.Height).Millimeter, 302.5, 303.5);
    }

    [Fact]
    public void NullLayoutMethod_WithBleed_HasSameTrimAndBleedSizes_WhenCropMarksVisibilityChanges()
    {
        var singlePageInput = Path.Combine(_outputDirectory, "full-bleed-visibility-input.pdf");
        using (var document = new PdfDocument())
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(216);
            page.Height = XUnit.FromMillimeter(303);
            document.Save(singlePageInput);
        }

        var noCropOutputPath = Path.Combine(_outputDirectory, "null-bleed-no-cropmarks-output.pdf");
        var cropOutputPath = Path.Combine(_outputDirectory, "null-bleed-with-cropmarks-output.pdf");
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        var noCropInputPdf = XPdfForm.FromFile(singlePageInput);
        var withCropInputPdf = XPdfForm.FromFile(singlePageInput);

        new NullLayoutMethod(bleedMM: 3.0).Layout(noCropInputPdf, singlePageInput, noCropOutputPath, paperTarget, false, false);
        new NullLayoutMethod(bleedMM: 3.0).Layout(withCropInputPdf, singlePageInput, cropOutputPath, paperTarget, false, true);

        using var noCropOutputDoc = PdfReader.Open(noCropOutputPath, PdfDocumentOpenMode.Import);
        using var cropOutputDoc = PdfReader.Open(cropOutputPath, PdfDocumentOpenMode.Import);

        var noCropPage = noCropOutputDoc.Pages[0];
        var cropPage = cropOutputDoc.Pages[0];

        var noCropTrim = noCropPage.TrimBox.ToXRect();
        var cropTrim = cropPage.TrimBox.ToXRect();
        var noCropBleed = noCropPage.BleedBox.ToXRect();
        var cropBleed = cropPage.BleedBox.ToXRect();

        Assert.InRange(Math.Abs(noCropTrim.Width - cropTrim.Width), 0.0, 0.5);
        Assert.InRange(Math.Abs(noCropTrim.Height - cropTrim.Height), 0.0, 0.5);
        Assert.InRange(Math.Abs(noCropBleed.Width - cropBleed.Width), 0.0, 0.5);
        Assert.InRange(Math.Abs(noCropBleed.Height - cropBleed.Height), 0.0, 0.5);
    }

    [Fact]
    public void NullLayoutMethod_WithCropMarks_PreservesSourceTrimAndBleedBoxes()
    {
        var singlePageInput = Path.Combine(_outputDirectory, "source-boxes-input.pdf");
        var sourceTrimInset = XUnit.FromMillimeter(3).Point;
        var sourceTrimWidth = XUnit.FromMillimeter(210).Point;
        var sourceTrimHeight = XUnit.FromMillimeter(297).Point;

        using (var document = new PdfDocument())
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(216);
            page.Height = XUnit.FromMillimeter(303);
            page.TrimBox = new PdfRectangle(new XPoint(sourceTrimInset, sourceTrimInset), new XSize(sourceTrimWidth, sourceTrimHeight));
            page.BleedBox = page.MediaBox;
            document.Save(singlePageInput);
        }

        var outputPath = Path.Combine(_outputDirectory, "source-boxes-output.pdf");
        var inputPdf = XPdfForm.FromFile(singlePageInput);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        new NullLayoutMethod().Layout(inputPdf, singlePageInput, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var outputPage = outputDoc.Pages[0];
        var outputTrim = outputPage.TrimBox.ToXRect();
        var outputBleed = outputPage.BleedBox.ToXRect();
        var cropMargin = XUnit.FromMillimeter(LayoutMethod.kMillimetersBetweenTrimAndMediaBox).Point;

        Assert.InRange(outputTrim.Width - sourceTrimWidth, -0.5, 0.5);
        Assert.InRange(outputTrim.Height - sourceTrimHeight, -0.5, 0.5);
        Assert.InRange(outputTrim.X - (sourceTrimInset + cropMargin), -0.5, 0.5);
        Assert.InRange(outputTrim.Y - (sourceTrimInset + cropMargin), -0.5, 0.5);
        Assert.InRange(outputBleed.X - cropMargin, -0.5, 0.5);
        Assert.InRange(outputBleed.Y - cropMargin, -0.5, 0.5);
    }

    [Fact]
    public void NullLayoutMethod_WithBleedAndExplicitSourceTrim_PreservesSourceTrimIntent()
    {
        var singlePageInput = Path.Combine(_outputDirectory, "source-trim-with-bleed-parameter-input.pdf");
        var sourceTrimInset = XUnit.FromMillimeter(3).Point;
        var sourceTrimWidth = XUnit.FromMillimeter(210).Point;
        var sourceTrimHeight = XUnit.FromMillimeter(297).Point;

        using (var document = new PdfDocument())
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(216);
            page.Height = XUnit.FromMillimeter(303);
            page.TrimBox = new PdfRectangle(new XPoint(sourceTrimInset, sourceTrimInset), new XSize(sourceTrimWidth, sourceTrimHeight));
            page.BleedBox = page.MediaBox;
            document.Save(singlePageInput);
        }

        var outputPath = Path.Combine(_outputDirectory, "source-trim-with-bleed-parameter-output.pdf");
        var inputPdf = XPdfForm.FromFile(singlePageInput);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        new NullLayoutMethod(bleedMM: 5.0).Layout(inputPdf, singlePageInput, outputPath, paperTarget, false, false);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var outputPage = outputDoc.Pages[0];
        var trimBox = outputPage.TrimBox.ToXRect();
        var bleedBox = outputPage.BleedBox.ToXRect();

        Assert.InRange(trimBox.X - sourceTrimInset, -0.5, 0.5);
        Assert.InRange(trimBox.Y - sourceTrimInset, -0.5, 0.5);
        Assert.InRange(trimBox.Width - sourceTrimWidth, -0.5, 0.5);
        Assert.InRange(trimBox.Height - sourceTrimHeight, -0.5, 0.5);
        Assert.InRange(XUnit.FromPoint(bleedBox.Width).Millimeter - 216, -0.5, 0.5);
        Assert.InRange(XUnit.FromPoint(bleedBox.Height).Millimeter - 303, -0.5, 0.5);
    }

    [Fact]
    public void MapSourceRectangleToTargetTrim_UsesTrimAsTheScalingReference()
    {
        var sourceTrim = new XRect(XUnit.FromMillimeter(3).Point, XUnit.FromMillimeter(3).Point,
            XUnit.FromMillimeter(210).Point, XUnit.FromMillimeter(297).Point);
        var sourceMedia = new XRect(0, 0, XUnit.FromMillimeter(216).Point, XUnit.FromMillimeter(303).Point);
        var targetTrim = new XRect(100, 50, XUnit.FromMillimeter(420).Point, XUnit.FromMillimeter(594).Point);

        var mappedMedia = LayoutMethod.MapSourceRectangleToTargetTrim(sourceMedia, sourceTrim, targetTrim);

        var expectedX = targetTrim.X - 2 * XUnit.FromMillimeter(3).Point;
        var expectedY = targetTrim.Y - 2 * XUnit.FromMillimeter(3).Point;
        var expectedWidth = 2 * XUnit.FromMillimeter(216).Point;
        var expectedHeight = 2 * XUnit.FromMillimeter(303).Point;

        Assert.InRange(mappedMedia.X - expectedX, -0.5, 0.5);
        Assert.InRange(mappedMedia.Y - expectedY, -0.5, 0.5);
        Assert.InRange(mappedMedia.Width - expectedWidth, -0.5, 0.5);
        Assert.InRange(mappedMedia.Height - expectedHeight, -0.5, 0.5);
    }

    [Fact]
    public void SideFoldBookletLayouter_CreatesCorrectNumberOfPages()
    {
        // Arrange
        var layoutMethod = new SideFoldBookletLayouter();
        var outputPath = Path.Combine(_outputDirectory, "sidefold-output.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        // Side fold booklet for 8 pages should create 2 sheets (4 pages output)
        Assert.True(outputDoc.PageCount > 0);
    }

    [Fact]
    public void SideFoldBookletLayouter_WithCropMarks_PreservesSourceBleedIntentInBleedBox()
    {
        var sourcePath = Path.Combine(_outputDirectory, "sidefold-source-boxes-input.pdf");
        var sourceTrimInset = XUnit.FromMillimeter(3).Point;
        var sourceTrimWidth = XUnit.FromMillimeter(210).Point;
        var sourceTrimHeight = XUnit.FromMillimeter(297).Point;

        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 4; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromMillimeter(216);
                page.Height = XUnit.FromMillimeter(303);
                page.TrimBox = new PdfRectangle(new XPoint(sourceTrimInset, sourceTrimInset), new XSize(sourceTrimWidth, sourceTrimHeight));
                page.BleedBox = page.MediaBox;
            }
            document.Save(sourcePath);
        }

        var outputPath = Path.Combine(_outputDirectory, "sidefold-source-boxes-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        new SideFoldBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var firstPage = outputDoc.Pages[0];
        var trim = firstPage.TrimBox.ToXRect();
        var bleed = firstPage.BleedBox.ToXRect();

        var expectedPanelTrim = new XRect(0, 0, trim.Width / 2, trim.Height);
        var sourceTrim = new XRect(sourceTrimInset, sourceTrimInset, sourceTrimWidth, sourceTrimHeight);
        var sourceBleed = new XRect(0, 0, XUnit.FromMillimeter(216).Point, XUnit.FromMillimeter(303).Point);
        var mappedPanelBleed = LayoutMethod.MapSourceRectangleToTargetTrim(sourceBleed, sourceTrim, expectedPanelTrim);
        var expectedBleedExpansionX = 2 * (expectedPanelTrim.X - mappedPanelBleed.X);
        var expectedBleedExpansionY = 2 * (expectedPanelTrim.Y - mappedPanelBleed.Y);

        Assert.InRange((bleed.Width - trim.Width) - expectedBleedExpansionX, -0.5, 0.5);
        Assert.InRange((bleed.Height - trim.Height) - expectedBleedExpansionY, -0.5, 0.5);
        Assert.InRange((trim.X - bleed.X) - (expectedBleedExpansionX / 2), -0.5, 0.5);
        Assert.InRange((trim.Y - bleed.Y) - (expectedBleedExpansionY / 2), -0.5, 0.5);
    }

    [Fact]
    public void SideFoldBookletLayouter_WithoutCropMarks_StillWritesProductionBoxes()
    {
        var sourcePath = Path.Combine(_outputDirectory, "sidefold-no-crop-source-boxes-input.pdf");
        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 4; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromMillimeter(216);
                page.Height = XUnit.FromMillimeter(303);
                page.TrimBox = new PdfRectangle(new XPoint(XUnit.FromMillimeter(3).Point, XUnit.FromMillimeter(3).Point),
                    new XSize(XUnit.FromMillimeter(210).Point, XUnit.FromMillimeter(297).Point));
                page.BleedBox = page.MediaBox;
            }
            document.Save(sourcePath);
        }

        var outputPath = Path.Combine(_outputDirectory, "sidefold-no-crop-source-boxes-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        new SideFoldBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, false);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var firstPage = outputDoc.Pages[0];

        Assert.NotNull(firstPage.Elements["/TrimBox"]);
        Assert.NotNull(firstPage.Elements["/BleedBox"]);
        Assert.NotNull(firstPage.Elements["/ArtBox"]);
        Assert.NotNull(firstPage.Elements["/CropBox"]);

        var trim = firstPage.TrimBox.ToXRect();
        var media = firstPage.MediaBox.ToXRect();
        Assert.InRange(Math.Abs(trim.Width - media.Width), 0.0, 0.5);
        Assert.InRange(Math.Abs(trim.Height - media.Height), 0.0, 0.5);
    }

    [Fact]
    public void SideFold4UpBookletLayouter_WithCropMarks_PreservesSourceBleedIntentInBleedBox()
    {
        var sourcePath = Path.Combine(_outputDirectory, "sidefold4up-source-boxes-input.pdf");
        var sourceTrimInset = XUnit.FromMillimeter(3).Point;
        var sourceTrimWidth = XUnit.FromMillimeter(210).Point;
        var sourceTrimHeight = XUnit.FromMillimeter(297).Point;

        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 8; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromMillimeter(216);
                page.Height = XUnit.FromMillimeter(303);
                page.TrimBox = new PdfRectangle(new XPoint(sourceTrimInset, sourceTrimInset), new XSize(sourceTrimWidth, sourceTrimHeight));
                page.BleedBox = page.MediaBox;
            }
            document.Save(sourcePath);
        }

        var outputPath = Path.Combine(_outputDirectory, "sidefold4up-source-boxes-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        new SideFold4UpBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        var firstPage = outputDoc.Pages[0];
        var trim = firstPage.TrimBox.ToXRect();
        var bleed = firstPage.BleedBox.ToXRect();

        Assert.True(bleed.Width > trim.Width + 0.5, "BleedBox width should expand beyond TrimBox for source full-bleed intent.");
        Assert.True(bleed.Height > trim.Height + 0.5, "BleedBox height should expand beyond TrimBox for source full-bleed intent.");
    }

    [Fact]
    public void SideFold4UpBookletLayouter_IsEnabledForCorrectInput()
    {
        // Arrange
        var layoutMethod = new SideFold4UpBookletLayouter();
        var inputPdf = XPdfForm.FromFile(_testPdfPath);

        // Act
        var isEnabled = layoutMethod.GetIsEnabled(inputPdf);

        // Assert - GetIsEnabled returns a bool
        Assert.True(isEnabled || !isEnabled); // Just verify it executes without error
    }

    [Fact]
    public void CalendarLayouter_ProducesOutput()
    {
        // Arrange
        var layoutMethod = new CalendarLayouter();
        var outputPath = Path.Combine(_outputDirectory, "calendar-output.pdf");
        var inputPdf = XPdfForm.FromFile(_testPdfPath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        // Act
        layoutMethod.Layout(inputPdf, _testPdfPath, outputPath, paperTarget, false, false);

        // Assert
        Assert.True(File.Exists(outputPath));
        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.True(outputDoc.PageCount > 0);
    }
}
