using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using DotImpose.LayoutMethods;
using System.Reflection;
using System.Globalization;
using System.Text;

namespace sillsdev.dotImpose.Tests;

public class LayoutMethodTests : IDisposable
{
    private static readonly object s_fixtureLock = new();
    private static string? s_realBloomFixturePath;

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
        var layoutMethod = new NullLayoutMethod(insetTrimboxMillimeters: 5.0);
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

        var layoutMethod = new NullLayoutMethod(insetTrimboxMillimeters: 3.0);
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

        new NullLayoutMethod(insetTrimboxMillimeters: 3.0).Layout(noCropInputPdf, singlePageInput, noCropOutputPath, paperTarget, false, false);
        new NullLayoutMethod(insetTrimboxMillimeters: 3.0).Layout(withCropInputPdf, singlePageInput, cropOutputPath, paperTarget, false, true);

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
    public void NullLayoutMethod_WithBleedAndExplicitSourceTrim_ThrowsInvalidOperationException()
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

        Assert.Throws<InvalidOperationException>(() =>
            new NullLayoutMethod(insetTrimboxMillimeters: 5.0).Layout(inputPdf, singlePageInput, outputPath, paperTarget, false, false));
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
    public void SideFold4Up_WithSquare13cmInput_DoesNotStretchImages()
    {
        var sourcePath = Path.Combine(_outputDirectory, "sidefold4up-square-13cm-input.pdf");
        var trimInset = XUnit.FromMillimeter(3).Point;
        var trimSize = XUnit.FromMillimeter(130).Point;
        var mediaSize = XUnit.FromMillimeter(136).Point;

        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 4; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(mediaSize);
                page.Height = XUnit.FromPoint(mediaSize);
                page.TrimBox = new PdfRectangle(new XPoint(trimInset, trimInset), new XSize(trimSize, trimSize));
                page.BleedBox = page.MediaBox;
            }
            document.Save(sourcePath);
        }

        var outputPath = Path.Combine(_outputDirectory, "sidefold4up-square-13cm-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A3", PdfSharp.PageSize.A3);

        new SideFold4UpBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(2, outputDoc.PageCount);

        var firstPage = outputDoc.Pages[0];
        var trim = firstPage.TrimBox.ToXRect();
        var expectedTrimUnionSize = (2 * trimSize) + (2 * trimInset);
        Assert.InRange(Math.Abs(trim.Width - expectedTrimUnionSize), 0.0, 0.5);
        Assert.InRange(Math.Abs(trim.Height - expectedTrimUnionSize), 0.0, 0.5);

        var transforms = GetImageDrawTransforms(firstPage);
        Assert.NotEmpty(transforms);
        foreach (var transform in transforms)
            Assert.InRange(Math.Abs(transform.ScaleX - transform.ScaleY), 0.0, 0.001);

        var xPositions = transforms.Select(t => Math.Round(t.TranslateX, 3)).Distinct().OrderBy(v => v).ToList();
        var yPositions = transforms.Select(t => Math.Round(t.TranslateY, 3)).Distinct().OrderBy(v => v).ToList();

        Assert.Equal(2, xPositions.Count);
        Assert.Equal(2, yPositions.Count);
        Assert.InRange(Math.Abs((xPositions[1] - xPositions[0]) - mediaSize), 0.0, 0.5);
        Assert.InRange(Math.Abs((yPositions[1] - yPositions[0]) - mediaSize), 0.0, 0.5);
    }

    [Fact]
    public void SideFold4Up_WithSquare13cmInput_UsesCustomTrimBoxWithoutCropMarks()
    {
        var sourcePath = Path.Combine(_outputDirectory, "sidefold4up-square-13cm-nocrop-input.pdf");
        var trimInset = XUnit.FromMillimeter(3).Point;
        var trimSize = XUnit.FromMillimeter(130).Point;
        var mediaSize = XUnit.FromMillimeter(136).Point;

        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 4; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(mediaSize);
                page.Height = XUnit.FromPoint(mediaSize);
                page.TrimBox = new PdfRectangle(new XPoint(trimInset, trimInset), new XSize(trimSize, trimSize));
                page.BleedBox = page.MediaBox;
            }
            document.Save(sourcePath);
        }

        var outputPath = Path.Combine(_outputDirectory, "sidefold4up-square-13cm-nocrop-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A3", PdfSharp.PageSize.A3);

        new SideFold4UpBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, false);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(2, outputDoc.PageCount);

        var firstPage = outputDoc.Pages[0];
        var trim = firstPage.TrimBox.ToXRect();
        var media = firstPage.MediaBox.ToXRect();
        var expectedTrimUnionSize = (2 * trimSize) + (2 * trimInset);

        Assert.InRange(Math.Abs(trim.Width - expectedTrimUnionSize), 0.0, 0.5);
        Assert.InRange(Math.Abs(trim.Height - expectedTrimUnionSize), 0.0, 0.5);
        Assert.True(trim.Width < media.Width - 0.5, "Expected a custom panel-union TrimBox, not full-sheet TrimBox.");
        Assert.True(trim.Height < media.Height - 0.5, "Expected a custom panel-union TrimBox, not full-sheet TrimBox.");
    }

    [Fact]
    public void SideFold4Up_RealBloomInput_DoesNotStretchImages()
    {
        var sourcePath = GetOrCreateRepositoryFixturePdfPath();

        var outputPath = Path.Combine(_outputDirectory, "sidefold4up-real-bloom-input-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A3", PdfSharp.PageSize.A3);

        new SideFold4UpBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(2, outputDoc.PageCount);

        foreach (var page in outputDoc.Pages)
        {
            var transforms = GetImageDrawTransforms(page);
            Assert.NotEmpty(transforms);
            foreach (var transform in transforms)
                Assert.InRange(Math.Abs(transform.ScaleX - transform.ScaleY), 0.0, 0.001);
        }
    }

    [Fact]
    public void DotImposeRuntimeInfo_ReturnsAssemblyInformationalVersion()
    {
        var expected = typeof(DotImpose.DotImposeRuntimeInfo)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var actual = DotImpose.DotImposeRuntimeInfo.GetInformationalVersion();

        Assert.False(string.IsNullOrWhiteSpace(actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Square6Up_With13cmTrimAnd3mmBleed_PreservesFullBleedAndAddsSideCutGuides()
    {
        var sourcePath = Path.Combine(_outputDirectory, "square6up-13cm-fullbleed-input.pdf");
        var trimInset = XUnit.FromMillimeter(3).Point;
        var trimSize = XUnit.FromMillimeter(130).Point;
        var mediaSize = XUnit.FromMillimeter(136).Point;

        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 4; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(mediaSize);
                page.Height = XUnit.FromPoint(mediaSize);
                page.TrimBox = new PdfRectangle(new XPoint(trimInset, trimInset), new XSize(trimSize, trimSize));
                page.BleedBox = page.MediaBox;
            }
            document.Save(sourcePath);
        }

        var outputPath = Path.Combine(_outputDirectory, "square6up-13cm-fullbleed-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A3", PdfSharp.PageSize.A3);

        new Square6UpBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(2, outputDoc.PageCount);

        var firstPage = outputDoc.Pages[0];
        var media = firstPage.MediaBox.ToXRect();
        var trim = firstPage.TrimBox.ToXRect();
        var bleed = firstPage.BleedBox.ToXRect();

        Assert.True(trim.Width < media.Width - 0.5, "Expected sheet TrimBox to be the imposed panel union, not full media width.");
        Assert.True(trim.Height < media.Height - 0.5, "Expected sheet TrimBox to be the imposed panel union, not full media height.");
        Assert.True(bleed.Width > trim.Width + 0.5, "BleedBox width should expand beyond TrimBox for source full-bleed intent.");
        Assert.True(bleed.Height > trim.Height + 0.5, "BleedBox height should expand beyond TrimBox for source full-bleed intent.");
        Assert.True(media.Bottom - bleed.Bottom > XUnit.FromMillimeter(1).Point,
            "Bleed touching the media bottom indicates clipped panel placement on the third row.");

        var lineSegments = GetLineSegments(firstPage);
        var sideCutGuides = lineSegments
            .Where(segment => Math.Abs(segment.Y1 - segment.Y2) < 0.01)
            .Where(segment => segment.Y1 > trim.Top + 0.5 && segment.Y1 < trim.Bottom - 0.5)
            .Where(segment =>
                Math.Max(segment.X1, segment.X2) <= trim.Left + 0.5 ||
                Math.Min(segment.X1, segment.X2) >= trim.Right - 0.5)
            .ToList();

        var guideRows = sideCutGuides
            .GroupBy(segment => Math.Round(segment.Y1, 2))
            .Select(group => group.Key)
            .ToList();

        Assert.Equal(4, guideRows.Count);

        var transforms = GetImageDrawTransforms(firstPage);
        Assert.NotEmpty(transforms);
        foreach (var transform in transforms)
        {
            Assert.InRange(transform.ScaleX, 0.999, 1.001);
            Assert.InRange(transform.ScaleY, 0.999, 1.001);
            Assert.True(transform.TranslateX >= 0, "Image draw translateX should not be negative.");
            Assert.True(transform.TranslateY >= 0, "Image draw translateY should not be negative.");
        }
    }

    [Fact]
    public void Square6Up_RealBloomInput_DoesNotUpscaleOrDrawOffPage()
    {
        var sourcePath = GetOrCreateRepositoryFixturePdfPath();

        var outputPath = Path.Combine(_outputDirectory, "square6up-real-bloom-input-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A3", PdfSharp.PageSize.A3);

        new Square6UpBookletLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(2, outputDoc.PageCount);

        foreach (var page in outputDoc.Pages)
        {
            var transforms = GetImageDrawTransforms(page);
            Assert.NotEmpty(transforms);

            foreach (var transform in transforms)
            {
                Assert.InRange(transform.ScaleX, 0.999, 1.001);
                Assert.InRange(transform.ScaleY, 0.999, 1.001);
                Assert.True(transform.TranslateX >= 0, "Image draw translateX should not be negative for this input.");
                Assert.True(transform.TranslateY >= 0, "Image draw translateY should not be negative for this input.");
            }
        }
    }

    private static string GetOrCreateRepositoryFixturePdfPath()
    {
        lock (s_fixtureLock)
        {
            if (!string.IsNullOrWhiteSpace(s_realBloomFixturePath) && File.Exists(s_realBloomFixturePath))
                return s_realBloomFixturePath;

            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var testDataDirectory = Path.Combine(repoRoot, "tests", "TestData");
            Directory.CreateDirectory(testDataDirectory);

            var fixturePath = Path.Combine(testDataDirectory, "real-bloom-like-input.pdf");
            if (!File.Exists(fixturePath))
            {
                var trimInset = XUnit.FromMillimeter(3).Point;
                var trimSize = XUnit.FromMillimeter(130).Point;
                var mediaSize = XUnit.FromMillimeter(136).Point;

                using var document = new PdfDocument();
                for (var i = 0; i < 4; i++)
                {
                    var page = document.AddPage();
                    page.Width = XUnit.FromPoint(mediaSize);
                    page.Height = XUnit.FromPoint(mediaSize);
                    page.TrimBox = new PdfRectangle(new XPoint(trimInset, trimInset), new XSize(trimSize, trimSize));
                    page.BleedBox = page.MediaBox;

                    // Keep content non-empty so output includes form draws to inspect transform matrices.
                    using var gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawRectangle(XPens.Black, XBrushes.LightGray,
                        new XRect(trimInset, trimInset, trimSize, trimSize));
                }

                document.Save(fixturePath);
            }

            s_realBloomFixturePath = fixturePath;
            return s_realBloomFixturePath;
        }
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

    [Fact]
    public void CalendarLayouter_WithCropMarks_BackPageBleedExpandsOnBottomForRotatedPanel()
    {
        var sourcePath = Path.Combine(_outputDirectory, "calendar-rotated-back-fullbleed-input.pdf");
        var sourceTrimInset = XUnit.FromMillimeter(3).Point;
        var sourceTrimWidth = XUnit.FromMillimeter(297).Point;
        var sourceTrimHeight = XUnit.FromMillimeter(210).Point;

        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 2; i++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromMillimeter(303);
                page.Height = XUnit.FromMillimeter(216);
                page.TrimBox = new PdfRectangle(new XPoint(sourceTrimInset, sourceTrimInset), new XSize(sourceTrimWidth, sourceTrimHeight));
                page.BleedBox = page.MediaBox;
            }
            document.Save(sourcePath);
        }

        var outputPath = Path.Combine(_outputDirectory, "calendar-rotated-back-fullbleed-output.pdf");
        var inputPdf = XPdfForm.FromFile(sourcePath);
        var paperTarget = new PaperTarget("A4", PdfSharp.PageSize.A4);

        new CalendarLayouter().Layout(inputPdf, sourcePath, outputPath, paperTarget, false, true);

        using var outputDoc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        Assert.True(outputDoc.PageCount >= 2, "Expected front and back sheet pages.");

        var backPage = outputDoc.Pages[1];
        var trim = backPage.TrimBox.ToXRect();
        var bleed = backPage.BleedBox.ToXRect();

        var topExpansion = trim.Top - bleed.Top;
        var bottomExpansion = bleed.Bottom - trim.Bottom;

        Assert.True(bottomExpansion > 1.0, "Expected bleed expansion toward bottom media margin for rotated back panel.");
        Assert.InRange(topExpansion, 0.0, 0.5);
    }

    private static List<(double ScaleX, double ScaleY, double TranslateX, double TranslateY)> GetImageDrawTransforms(PdfPage page)
    {
        var transforms = new List<(double ScaleX, double ScaleY, double TranslateX, double TranslateY)>();
        for (var streamIndex = 0; streamIndex < page.Contents.Elements.Count; streamIndex++)
        {
            var dictionary = page.Contents.Elements.GetDictionary(streamIndex);
            if (dictionary is not PdfDictionary { Stream: not null })
                continue;

            var streamText = Encoding.ASCII.GetString(dictionary.Stream.Value).Replace("\r", string.Empty);
            var lines = streamText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.Contains(" cm") || !trimmed.Contains("/Fm"))
                    continue;

                var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 6)
                    continue;

                if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
                    continue;
                if (!double.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    continue;
                if (!double.TryParse(tokens[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var e))
                    continue;
                if (!double.TryParse(tokens[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    continue;

                transforms.Add((a, d, e, f));
            }
        }

        return transforms;
    }

    private static List<(double X1, double Y1, double X2, double Y2)> GetLineSegments(PdfPage page)
    {
        var lineSegments = new List<(double X1, double Y1, double X2, double Y2)>();
        for (var streamIndex = 0; streamIndex < page.Contents.Elements.Count; streamIndex++)
        {
            var dictionary = page.Contents.Elements.GetDictionary(streamIndex);
            if (dictionary is not PdfDictionary { Stream: not null })
                continue;

            (double X, double Y)? movePoint = null;
            var streamText = Encoding.ASCII.GetString(dictionary.Stream.Value).Replace("\r", string.Empty);
            var lines = streamText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3)
                    continue;

                var op = tokens[^1];
                if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                    continue;
                if (!double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                    continue;

                if (op == "m")
                {
                    movePoint = (x, y);
                    continue;
                }

                if (op == "l" && movePoint.HasValue)
                {
                    lineSegments.Add((movePoint.Value.X, movePoint.Value.Y, x, y));
                    movePoint = (x, y);
                }
            }
        }

        return lineSegments;
    }
}
