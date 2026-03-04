using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace DotImpose.LayoutMethods
{
	/// <summary>
	/// Abstract base class for PDF layout methods that rearrange pages for various printing and binding purposes.
	/// </summary>
	public abstract class LayoutMethod
	{
		/// <summary>
		/// Source page geometry used to map source trim/bleed intent to an imposed panel.
		/// </summary>
		protected readonly struct SourcePageBoxes
		{
			/// <summary>
			/// Initializes a new instance of source page geometry.
			/// </summary>
			public SourcePageBoxes(XRect mediaBox, XRect trimBox, XRect bleedBox, bool hasExplicitTrimBox = false, bool hasExplicitBleedBox = false)
			{
				MediaBox = mediaBox;
				TrimBox = trimBox;
				BleedBox = bleedBox;
				HasExplicitTrimBox = hasExplicitTrimBox;
				HasExplicitBleedBox = hasExplicitBleedBox;
			}

			/// <summary>
			/// Gets the source media box.
			/// </summary>
			public XRect MediaBox { get; }
			/// <summary>
			/// Gets the source trim box.
			/// </summary>
			public XRect TrimBox { get; }
			/// <summary>
			/// Gets the source bleed box.
			/// </summary>
			public XRect BleedBox { get; }
			/// <summary>
			/// Gets whether the source page explicitly defined a trim box.
			/// </summary>
			public bool HasExplicitTrimBox { get; }
			/// <summary>
			/// Gets whether the source page explicitly defined a bleed box.
			/// </summary>
			public bool HasExplicitBleedBox { get; }
		}

		/// <summary>
		/// Gets the identifier for this layout method (e.g., "cutBooklet", "sideFoldBooklet").
		/// </summary>
		public string Id { get; }

		/// <summary>
		/// Gets the English label for this layout method (e.g., "Cut and Stack", "Fold Booklet").
		/// </summary>
		public string EnglishLabel { get; }

		/// <summary>
		/// The width of the output paper.
		/// </summary>
		protected XUnit _paperWidth;

		/// <summary>
		/// The height of the output paper.
		/// </summary>
		protected XUnit _paperHeight;

		/// <summary>
		/// The input PDF form being processed.
		/// </summary>
		protected XPdfForm _inputPdf;

		/// <summary>
		/// Indicates whether the layout is for right-to-left languages.
		/// </summary>
		protected bool _rightToLeft;

		/// <summary>
		/// Indicates whether the layout is in calendar mode.
		/// </summary>
		protected bool _calendarMode;

		/// <summary>
		/// Indicates whether to show crop marks on the output.
		/// </summary>
		protected bool _showCropMarks;
		private readonly List<SourcePageBoxes> _sourcePageBoxes = new List<SourcePageBoxes>();
		private string _sourcePageBoxesInputPath = string.Empty;
		private PdfPage _activeOutputPage;
		private double _activeCropMarkMarginPoints;

		/// <summary>
		/// Distance in millimeters between trim box and media box for crop marks (6mm standard).
		/// </summary>
		public const double kMillimetersBetweenTrimAndMediaBox = 6; //I read that "3.175" is standard, but then the crop marks are barely visible. I'm concerned that if they aren't obvious, people might not understand what they are seeing, and be confused.

		/// <summary>
		/// Initializes a new instance of the LayoutMethod class.
		/// </summary>
		/// <param name="id">The identifier for this layout method.</param>
		/// <param name="englishLabel">The English label for this layout method.</param>
		protected LayoutMethod(string id, string englishLabel)
		{
			Id = id;
			EnglishLabel = englishLabel;
		}

		/// <summary>
		/// Gets a value indicating whether this layout method's output is affected by page orientation.
		/// </summary>
		public virtual bool ImageIsSensitiveToOrientation
		{
			get { return false; }
		}

		/// <summary>
		/// Produce a new pdf with rearranged pages
		/// </summary>
		/// <param name="inputPdf">the source pdf</param>
		/// <param name="inputPath">the path to the source pdf (used by null layouter)</param>
		/// <param name="outputPath"></param>
		/// <param name="paperTarget">The size of the pages of the output pdf</param>
		/// <param name="rightToLeft">Is this a right-to-left language?  Might be better-named "backToFront"</param>
		/// <param name="showCropMarks">For commercial printing, enlarge MediaBox and draw crop marks around the sheet trim.</param>
		public virtual void Layout(XPdfForm inputPdf, string inputPath, string outputPath, PaperTarget paperTarget, bool rightToLeft, bool showCropMarks)
		{
			_rightToLeft = rightToLeft;
			_inputPdf = inputPdf;
			_showCropMarks = showCropMarks;
			EnsureSourcePageBoxesLoaded(inputPath);

			PdfDocument outputDocument = new PdfDocument();

			// Show single pages
			// (Note: one page contains two pages from the source document.
			//  If the number of pages of the source document can not be
			//  divided by 4, the first pages of the output document will
			//  each contain only one page from the source document.)
			outputDocument.PageLayout = PdfPageLayout.SinglePage;

			// Determine width and height
			SetPaperSize(paperTarget);


			int inputPages = _inputPdf.PageCount;
			int numberOfSheetsOfPaper = inputPages / 4;
			if (numberOfSheetsOfPaper * 4 < inputPages)
				numberOfSheetsOfPaper += 1;
			int numberOfPageSlotsAvailable = 4 * numberOfSheetsOfPaper;
			int vacats = numberOfPageSlotsAvailable - inputPages;

			LayoutInner(outputDocument, numberOfSheetsOfPaper, numberOfPageSlotsAvailable, vacats);

			//            if(true)
			//                foreach (PdfPage page in outputDocument.Pages)
			//                {
			//
			//                   var  gfx = XGraphics.FromPdfPage(page);
			//                    gfx.DrawImage(page, 0.0,0.0);
			//                    page.MediaBox = new PdfRectangle(new XPoint(m.X2, m.Y1), new XPoint(m.X1, m.Y2));
			//                }
			outputDocument.Save(outputPath);
		}

		/// <summary>
		/// Sets the paper size for the output document based on the paper target and input PDF dimensions.
		/// </summary>
		/// <param name="paperTarget">The target paper size specification.</param>
		protected virtual void SetPaperSize(PaperTarget paperTarget)
		{
			var dimensions = paperTarget.GetPaperDimensions(_inputPdf.PixelWidth, _inputPdf.PixelHeight);
			_paperWidth = XUnit.FromPoint(dimensions.X);
			_paperHeight = XUnit.FromPoint(dimensions.Y);
		}

		/// <summary>
		/// Loads source page box metadata from the input path for trim/bleed-aware imposition.
		/// </summary>
		/// <param name="inputPath">Path to the source PDF.</param>
		protected void EnsureSourcePageBoxesLoaded(string inputPath)
		{
			if (_sourcePageBoxesInputPath == inputPath && _sourcePageBoxes.Count == _inputPdf.PageCount)
				return;

			_sourcePageBoxes.Clear();
			_sourcePageBoxesInputPath = inputPath;

			if (!string.IsNullOrWhiteSpace(inputPath) && File.Exists(inputPath))
			{
				using (var sourceDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
				{
					for (var i = 0; i < sourceDocument.PageCount; i++)
					{
						var sourcePage = sourceDocument.Pages[i];
						var elements = sourcePage.Elements;
						var mediaBox = NormalizeBox(sourcePage.MediaBox.ToXRect());
						var hasExplicitTrimBox = elements != null && elements["/TrimBox"] != null;
						var hasExplicitBleedBox = elements != null && elements["/BleedBox"] != null;
						var trimBox = hasExplicitTrimBox
							? NormalizeBox(sourcePage.TrimBox.ToXRect())
							: mediaBox;
						var bleedBox = hasExplicitBleedBox
							? NormalizeBox(sourcePage.BleedBox.ToXRect())
							: mediaBox;

						trimBox = IntersectBoxes(trimBox, mediaBox);
						if (trimBox.Width <= 0 || trimBox.Height <= 0)
							trimBox = mediaBox;

						// Clamp bleed to media and ensure it still encloses trim intent.
						bleedBox = IntersectBoxes(bleedBox, mediaBox);
						if (bleedBox.Width <= 0 || bleedBox.Height <= 0)
							bleedBox = trimBox;
						bleedBox = UnionBoxes(bleedBox, trimBox);

						_sourcePageBoxes.Add(new SourcePageBoxes(mediaBox, trimBox, bleedBox, hasExplicitTrimBox, hasExplicitBleedBox));
					}
				}
			}

			if (_sourcePageBoxes.Count != _inputPdf.PageCount)
			{
				_sourcePageBoxes.Clear();
				var fallbackMedia = new XRect(0, 0, _inputPdf.PointWidth, _inputPdf.PointHeight);
				for (var i = 0; i < _inputPdf.PageCount; i++)
					_sourcePageBoxes.Add(new SourcePageBoxes(fallbackMedia, fallbackMedia, fallbackMedia));
			}
		}

		/// <summary>
		/// Gets source page geometry for a one-based page number.
		/// </summary>
		/// <param name="pageNumber">One-based source page index.</param>
		/// <returns>Resolved source page boxes, or a full-page fallback.</returns>
		protected SourcePageBoxes GetSourcePageBoxes(int pageNumber)
		{
			if (pageNumber <= 0 || pageNumber > _sourcePageBoxes.Count)
			{
				var fallbackMedia = new XRect(0, 0, _inputPdf.PointWidth, _inputPdf.PointHeight);
				return new SourcePageBoxes(fallbackMedia, fallbackMedia, fallbackMedia);
			}

			return _sourcePageBoxes[pageNumber - 1];
		}

		/// <summary>
		/// Draws a source page by mapping its source trim box to a target panel trim box.
		/// Bleed clipping is applied using the mapped source bleed box.
		/// </summary>
		/// <param name="gfx">Target graphics context.</param>
		/// <param name="pageNumber">One-based source page index.</param>
		/// <param name="targetTrimBox">Destination trim box in graphics coordinates.</param>
		protected void DrawPageUsingSourceTrimIntent(XGraphics gfx, int pageNumber, XRect targetTrimBox)
		{
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, targetTrimBox, targetTrimBox);
		}

		/// <summary>
		/// Draws a source page by mapping source trim to a graphics-space target trim box,
		/// while allowing a separate page-space trim box for metadata updates.
		/// </summary>
		/// <param name="gfx">Target graphics context.</param>
		/// <param name="pageNumber">One-based source page index.</param>
		/// <param name="drawTargetTrimBox">Destination trim box in graphics coordinates used for drawing.</param>
		/// <param name="metadataTargetTrimBox">Destination trim box in page coordinates used for Trim/Bleed metadata updates.</param>
		protected void DrawPageUsingSourceTrimIntent(XGraphics gfx, int pageNumber, XRect drawTargetTrimBox, XRect metadataTargetTrimBox)
		{
			var sourceBoxes = GetSourcePageBoxes(pageNumber);
			var mappedMedia = MapSourceRectangleToTargetTrim(sourceBoxes.MediaBox, sourceBoxes.TrimBox, drawTargetTrimBox);
			var mappedBleedForDrawing = MapSourceRectangleToTargetTrim(sourceBoxes.BleedBox, sourceBoxes.TrimBox, drawTargetTrimBox);
			var mappedBleedForMetadata = MapSourceRectangleToTargetTrim(sourceBoxes.BleedBox, sourceBoxes.TrimBox, metadataTargetTrimBox);
			UpdateActivePageBoxesForTrimIntent(sourceBoxes, metadataTargetTrimBox, mappedBleedForMetadata);

			var state = gfx.Save();
			gfx.IntersectClip(mappedBleedForDrawing);
			_inputPdf.PageNumber = pageNumber;
			gfx.DrawImage(_inputPdf, mappedMedia);
			gfx.Restore(state);
		}

		private void UpdateActivePageBoxesForTrimIntent(SourcePageBoxes sourceBoxes, XRect targetTrimBox, XRect mappedBleed)
		{
			if (_activeOutputPage == null)
				return;

			var mappedTrim = MapSourceRectangleToTargetTrim(sourceBoxes.TrimBox, sourceBoxes.TrimBox, targetTrimBox);

			if (_activeCropMarkMarginPoints > 0)
			{
				mappedTrim = OffsetBox(mappedTrim, _activeCropMarkMarginPoints, _activeCropMarkMarginPoints);
				mappedBleed = OffsetBox(mappedBleed, _activeCropMarkMarginPoints, _activeCropMarkMarginPoints);
			}

			var mediaRect = NormalizeBox(_activeOutputPage.MediaBox.ToXRect());
			mappedTrim = IntersectBoxes(mappedTrim, mediaRect);
			mappedBleed = IntersectBoxes(mappedBleed, mediaRect);

			if (mappedTrim.Width <= 0 || mappedTrim.Height <= 0)
				return;

			var currentTrim = _activeOutputPage.TrimBox.ToXRect();
			var currentBleed = _activeOutputPage.BleedBox.ToXRect();

			// Keep trim as a sheet-level box while widening bleed to include mapped source bleed intent.
			var updatedBleed = UnionBoxes(currentBleed, mappedBleed);
			updatedBleed = UnionBoxes(updatedBleed, currentTrim);
			updatedBleed = IntersectBoxes(updatedBleed, mediaRect);

			_activeOutputPage.TrimBox = ToPdfRectangle(currentTrim);
			_activeOutputPage.BleedBox = ToPdfRectangle(updatedBleed);
			_activeOutputPage.ArtBox = _activeOutputPage.TrimBox;
			_activeOutputPage.CropBox = _activeOutputPage.MediaBox;
		}

		/// <summary>
		/// Maps a source rectangle into output coordinates by using source trim as the scaling reference.
		/// </summary>
		/// <param name="sourceRectangle">Rectangle in source-page coordinates.</param>
		/// <param name="sourceTrimBox">Source trim box.</param>
		/// <param name="targetTrimBox">Target trim box.</param>
		/// <returns>The mapped rectangle in output coordinates.</returns>
		internal static XRect MapSourceRectangleToTargetTrim(XRect sourceRectangle, XRect sourceTrimBox, XRect targetTrimBox)
		{
			if (sourceTrimBox.Width <= 0 || sourceTrimBox.Height <= 0)
				throw new ArgumentException("sourceTrimBox must have a positive width and height", nameof(sourceTrimBox));

			var scaleX = targetTrimBox.Width / sourceTrimBox.Width;
			var scaleY = targetTrimBox.Height / sourceTrimBox.Height;

			var mappedX = targetTrimBox.X + (sourceRectangle.X - sourceTrimBox.X) * scaleX;
			var mappedY = targetTrimBox.Y + (sourceRectangle.Y - sourceTrimBox.Y) * scaleY;
			var mappedWidth = sourceRectangle.Width * scaleX;
			var mappedHeight = sourceRectangle.Height * scaleY;
			return new XRect(mappedX, mappedY, mappedWidth, mappedHeight);
		}

		/// <summary>
		/// Offsets a rectangle by the supplied x/y deltas.
		/// </summary>
		/// <param name="box">Rectangle to offset.</param>
		/// <param name="xOffset">Offset in x-axis (points).</param>
		/// <param name="yOffset">Offset in y-axis (points).</param>
		/// <returns>The offset rectangle.</returns>
		protected static XRect OffsetBox(XRect box, double xOffset, double yOffset)
		{
			return new XRect(box.X + xOffset, box.Y + yOffset, box.Width, box.Height);
		}

		/// <summary>
		/// Insets a rectangle equally on all sides.
		/// </summary>
		/// <param name="box">Rectangle to inset.</param>
		/// <param name="inset">Inset distance in points.</param>
		/// <returns>The inset rectangle, clamped to non-negative size.</returns>
		protected static XRect InsetBox(XRect box, double inset)
		{
			var width = Math.Max(0, box.Width - 2 * inset);
			var height = Math.Max(0, box.Height - 2 * inset);
			return new XRect(box.X + inset, box.Y + inset, width, height);
		}

		private static XRect NormalizeBox(XRect box)
		{
			var left = Math.Min(box.Left, box.Right);
			var top = Math.Min(box.Top, box.Bottom);
			var right = Math.Max(box.Left, box.Right);
			var bottom = Math.Max(box.Top, box.Bottom);
			return new XRect(left, top, right - left, bottom - top);
		}

		private static XRect IntersectBoxes(XRect first, XRect second)
		{
			var left = Math.Max(first.Left, second.Left);
			var top = Math.Max(first.Top, second.Top);
			var right = Math.Min(first.Right, second.Right);
			var bottom = Math.Min(first.Bottom, second.Bottom);

			if (right <= left || bottom <= top)
				return new XRect(0, 0, 0, 0);

			return new XRect(left, top, right - left, bottom - top);
		}

		private static XRect UnionBoxes(XRect first, XRect second)
		{
			var left = Math.Min(first.Left, second.Left);
			var top = Math.Min(first.Top, second.Top);
			var right = Math.Max(first.Right, second.Right);
			var bottom = Math.Max(first.Bottom, second.Bottom);
			return new XRect(left, top, right - left, bottom - top);
		}

		/// <summary>
		/// Performs the actual page layout logic. Must be implemented by derived classes.
		/// </summary>
		/// <param name="outputDocument">The output PDF document.</param>
		/// <param name="numberOfSheetsOfPaper">The number of sheets of paper needed.</param>
		/// <param name="numberOfPageSlotsAvailable">The total number of page slots available.</param>
		/// <param name="vacats">The number of vacant page slots.</param>
		protected abstract void LayoutInner(PdfDocument outputDocument, int numberOfSheetsOfPaper, int numberOfPageSlotsAvailable, int vacats);

		/// <summary>
		/// Creates a new page in the output document and returns its graphics context.
		/// </summary>
		/// <param name="outputDocument">The output PDF document.</param>
		/// <returns>The graphics context for the new page.</returns>
		protected XGraphics GetGraphicsForNewPage(PdfDocument outputDocument)
		{
			XGraphics gfx;
			PdfPage page = outputDocument.AddPage();
			//page.Orientation = PageOrientation.Landscape;//review: why does this say it's always landscape (and why does that work?) Or maybe it has no effect?

			var xunitsBetweenTrimAndMediaBox = XUnit.FromMillimeter(kMillimetersBetweenTrimAndMediaBox);
			_activeOutputPage = page;
			_activeCropMarkMarginPoints = _showCropMarks ? xunitsBetweenTrimAndMediaBox.Point : 0;
			var fullSheetRect = new XRect(0, 0, _paperWidth.Point, _paperHeight.Point);
			var sheetTrimRect = IntersectBoxes(NormalizeBox(GetSheetTrimBoxInPaperCoordinates()), fullSheetRect);
			if (sheetTrimRect.Width <= 0 || sheetTrimRect.Height <= 0)
				sheetTrimRect = fullSheetRect;

			if (_showCropMarks)
			{
				page.Width = XUnit.FromMillimeter(_paperWidth.Millimeter + (2.0 * kMillimetersBetweenTrimAndMediaBox));
				page.Height = XUnit.FromMillimeter(_paperHeight.Millimeter + (2.0 * kMillimetersBetweenTrimAndMediaBox));
				page.TrimBox = ToPdfRectangle(OffsetBox(sheetTrimRect, xunitsBetweenTrimAndMediaBox.Point, xunitsBetweenTrimAndMediaBox.Point));
			}
			else
			{
				page.Width = _paperWidth;
				page.Height = _paperHeight;
				page.TrimBox = ToPdfRectangle(sheetTrimRect);
			}

			page.BleedBox = page.TrimBox;
			page.ArtBox = page.TrimBox;
			page.CropBox = page.MediaBox;

			gfx = XGraphics.FromPdfPage(page);

			if (_showCropMarks)
			{
				DrawCropMarks(page, gfx, xunitsBetweenTrimAndMediaBox);
				//push the page down and to the left
				gfx.TranslateTransform(xunitsBetweenTrimAndMediaBox.Point, xunitsBetweenTrimAndMediaBox.Point);
			}

			// Mirror support removed - was UI-specific

			return gfx;
		}

		/// <summary>
		/// Gets the sheet trim rectangle in paper coordinates (before crop-mark margin offset).
		/// </summary>
		/// <returns>Trim rectangle in paper coordinates.</returns>
		protected virtual XRect GetSheetTrimBoxInPaperCoordinates()
		{
			return new XRect(0, 0, _paperWidth.Point, _paperHeight.Point);
		}

		/// <summary>
		/// Converts an <see cref="XRect"/> to a <see cref="PdfRectangle"/>.
		/// </summary>
		/// <param name="rect">Rectangle in point coordinates.</param>
		/// <returns>A PDF rectangle with the same position and size.</returns>
		protected static PdfRectangle ToPdfRectangle(XRect rect)
		{
			return new PdfRectangle(new XPoint(rect.X, rect.Y), new XSize(rect.Width, rect.Height));
		}

		/// <summary>
		/// Gets the PDF rectangle for the trim box with appropriate margins.
		/// </summary>
		/// <returns>The trim box rectangle.</returns>
		protected PdfRectangle GetTrimBoxRectangle()
		{
			var xunitsBetweenTrimAndMediaBox = XUnit.FromMillimeter(kMillimetersBetweenTrimAndMediaBox);
			var fullSheetRect = new XRect(0, 0, _paperWidth.Point, _paperHeight.Point);
			var sheetTrimRect = IntersectBoxes(NormalizeBox(GetSheetTrimBoxInPaperCoordinates()), fullSheetRect);
			if (sheetTrimRect.Width <= 0 || sheetTrimRect.Height <= 0)
				sheetTrimRect = fullSheetRect;
			return ToPdfRectangle(OffsetBox(sheetTrimRect, xunitsBetweenTrimAndMediaBox.Point, xunitsBetweenTrimAndMediaBox.Point));
		}

		/// <summary>
		/// Gets the left edge position for the superior (first) page on a sheet.
		/// Adjusts for right-to-left languages.
		/// </summary>
		protected double LeftEdgeForSuperiorPage
		{
			get { return _rightToLeft ? _paperWidth.Point / 2 : XUnit.FromPoint(0).Point; }
		}

		/// <summary>
		/// Gets the left edge position for the inferior (second) page on a sheet.
		/// Adjusts for right-to-left languages.
		/// </summary>
		protected double LeftEdgeForInferiorPage
		{
			get { return _rightToLeft ? XUnit.FromPoint(0).Point : _paperWidth.Point / 2; }
		}

		/// <summary>
		/// Draws crop marks around the current page trim box.
		/// </summary>
		/// <param name="page">Page that contains the trim box definition.</param>
		/// <param name="gfx">Graphics context used to draw the marks.</param>
		/// <param name="xunitsBetweenTrimAndMediaBox">Distance between trim and media boxes.</param>
		protected static void DrawCropMarks(PdfPage page, XGraphics gfx, XUnit xunitsBetweenTrimAndMediaBox)
		{
			XPoint upperLeftTrimBoxCorner = page.TrimBox.ToXRect().TopLeft;
			XPoint upperRightTrimBoxCorner = page.TrimBox.ToXRect().TopRight;
			XPoint lowerLeftTrimBoxCorner = page.TrimBox.ToXRect().BottomLeft;
			XPoint lowerRightTrimBoxCorner = page.TrimBox.ToXRect().BottomRight;

			//while blue would look nicer, then if they make color separations, the marks wouldn't show all all of them.
			//Note that in InDesign, there is a "registration color" which looks black but is actually 100% of all each
			//sep color, so it always prints. But I don't see a way to do that in PDF.
			//.25 is a standard width
			var pen = new XPen(XColor.FromKnownColor(XKnownColor.Black), .25);

			var gapLength = XUnit.FromMillimeter(3.175); // this 3.175 is the industry standard

			gfx.DrawLine(pen, XUnit.FromPoint(upperLeftTrimBoxCorner.X - gapLength.Point).Point, upperLeftTrimBoxCorner.Y,
						 XUnit.FromPoint(upperLeftTrimBoxCorner.X - xunitsBetweenTrimAndMediaBox.Point).Point, upperLeftTrimBoxCorner.Y);
			gfx.DrawLine(pen, upperLeftTrimBoxCorner.X, XUnit.FromPoint(upperLeftTrimBoxCorner.Y - gapLength.Point).Point, upperLeftTrimBoxCorner.X,
						 XUnit.FromPoint(upperLeftTrimBoxCorner.Y - xunitsBetweenTrimAndMediaBox.Point).Point);

			gfx.DrawLine(pen, XUnit.FromPoint(upperRightTrimBoxCorner.X + gapLength.Point).Point, upperRightTrimBoxCorner.Y,
						 XUnit.FromPoint(upperRightTrimBoxCorner.X + xunitsBetweenTrimAndMediaBox.Point).Point, upperRightTrimBoxCorner.Y);
			gfx.DrawLine(pen, upperRightTrimBoxCorner.X, XUnit.FromPoint(upperRightTrimBoxCorner.Y - gapLength.Point).Point, upperRightTrimBoxCorner.X,
						 XUnit.FromPoint(upperRightTrimBoxCorner.Y - xunitsBetweenTrimAndMediaBox.Point).Point);

			gfx.DrawLine(pen, XUnit.FromPoint(lowerLeftTrimBoxCorner.X - gapLength.Point).Point, lowerLeftTrimBoxCorner.Y,
						 XUnit.FromPoint(lowerLeftTrimBoxCorner.X - xunitsBetweenTrimAndMediaBox.Point).Point, lowerLeftTrimBoxCorner.Y);
			gfx.DrawLine(pen, lowerLeftTrimBoxCorner.X, XUnit.FromPoint(lowerLeftTrimBoxCorner.Y + gapLength.Point).Point, lowerLeftTrimBoxCorner.X,
						 XUnit.FromPoint(lowerLeftTrimBoxCorner.Y + xunitsBetweenTrimAndMediaBox.Point).Point);

			gfx.DrawLine(pen, XUnit.FromPoint(lowerRightTrimBoxCorner.X + gapLength.Point).Point, lowerRightTrimBoxCorner.Y,
						 XUnit.FromPoint(lowerRightTrimBoxCorner.X + xunitsBetweenTrimAndMediaBox.Point).Point, lowerRightTrimBoxCorner.Y);
			gfx.DrawLine(pen, lowerRightTrimBoxCorner.X, XUnit.FromPoint(lowerRightTrimBoxCorner.Y + gapLength.Point).Point, lowerRightTrimBoxCorner.X,
						 XUnit.FromPoint(lowerRightTrimBoxCorner.Y + xunitsBetweenTrimAndMediaBox.Point).Point);
		}

		/// <summary>
		/// Draws short internal cut guide segments on the left and right sides of the sheet trim.
		/// </summary>
		/// <param name="gfx">Graphics context used to draw the guides.</param>
		/// <param name="sheetTrimBox">Sheet trim box in graphics coordinates.</param>
		/// <param name="verticalCutX">Unused in booklet mode.</param>
		/// <param name="horizontalCutY">Horizontal cut y-position in graphics coordinates.</param>
		protected static void DrawCenterCutGuideSegments(XGraphics gfx, XRect sheetTrimBox, double verticalCutX, double horizontalCutY)
		{
			var pen = new XPen(XColor.FromKnownColor(XKnownColor.Black), .25);
			var outerLength = XUnit.FromMillimeter(kMillimetersBetweenTrimAndMediaBox).Point;
			var gapLength = XUnit.FromMillimeter(3.175).Point;

			var leftStart = sheetTrimBox.Left - outerLength;
			var leftEnd = sheetTrimBox.Left - gapLength;
			if (leftEnd > leftStart)
				gfx.DrawLine(pen, leftStart, horizontalCutY, leftEnd, horizontalCutY);

			var rightStart = sheetTrimBox.Right + gapLength;
			var rightEnd = sheetTrimBox.Right + outerLength;
			if (rightEnd > rightStart)
				gfx.DrawLine(pen, rightStart, horizontalCutY, rightEnd, horizontalCutY);
		}

		/// <summary>
		/// Determines whether this layout method is enabled for the given input PDF.
		/// </summary>
		/// <param name="inputPdf">The input PDF form to check.</param>
		/// <returns>True if this layout method can be used with the input PDF; otherwise, false.</returns>
		public abstract bool GetIsEnabled(XPdfForm inputPdf);

		/// <summary>
		/// Determines whether the input PDF is in landscape orientation.
		/// </summary>
		/// <param name="inputPdf">The input PDF form to check.</param>
		/// <returns>True if the PDF is landscape; otherwise, false.</returns>
		public static bool IsLandscape(XPdfForm inputPdf)
		{
			return inputPdf != null && inputPdf.PixelWidth > inputPdf.PixelHeight;
		}

		/// <summary>
		/// Determines whether the input PDF is in portrait orientation.
		/// </summary>
		/// <param name="inputPdf">The input PDF form to check.</param>
		/// <returns>True if the PDF is portrait; otherwise, false.</returns>
		public static bool IsPortrait(XPdfForm inputPdf)
		{
			return inputPdf != null && inputPdf.PixelWidth < inputPdf.PixelHeight;
		}

		/// <summary>
		/// Determines whether the input PDF has square pages.
		/// </summary>
		/// <param name="inputPdf">The input PDF form to check.</param>
		/// <returns>True if the PDF has square pages; otherwise, false.</returns>
		public static bool IsSquare(XPdfForm inputPdf)
		{
			return inputPdf != null && inputPdf.PixelWidth == inputPdf.PixelHeight;
		}
	}
}
