using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DotImpose.LayoutMethods
{
	/// <summary>
	/// Abstract base class for PDF layout methods that rearrange pages for various printing and binding purposes.
	/// </summary>
	public abstract class LayoutMethod
	{
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
		/// <param name="showCropMarks">For commercial printing, make a Trimbox, BleedBox, and crop marks</param>
		public virtual void Layout(XPdfForm inputPdf, string inputPath, string outputPath, PaperTarget paperTarget, bool rightToLeft, bool showCropMarks)
		{
			_rightToLeft = rightToLeft;
			_inputPdf = inputPdf;
			_showCropMarks = showCropMarks;

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

			if (_showCropMarks)
			{
				page.Width = XUnit.FromMillimeter(_paperWidth.Millimeter + (2.0 * kMillimetersBetweenTrimAndMediaBox));
				page.Height = XUnit.FromMillimeter(_paperHeight.Millimeter + (2.0 * kMillimetersBetweenTrimAndMediaBox)); ;
				page.TrimBox = GetTrimBoxRectangle();
				//page.CropBox = page.TrimBox;
			}
			else
			{
				page.Width = _paperWidth;
				page.Height = _paperHeight;
			}

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
		/// Gets the PDF rectangle for the trim box with appropriate margins.
		/// </summary>
		/// <returns>The trim box rectangle.</returns>
		protected PdfRectangle GetTrimBoxRectangle()
		{
			var xunitsBetweenTrimAndMediaBox = XUnit.FromMillimeter(kMillimetersBetweenTrimAndMediaBox);
			XPoint upperLeftTrimBoxCorner = new XPoint(xunitsBetweenTrimAndMediaBox.Point, xunitsBetweenTrimAndMediaBox.Point);
			return new PdfRectangle(upperLeftTrimBoxCorner, new XSize(_paperWidth.Point, _paperHeight.Point));
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

		private static void DrawCropMarks(PdfPage page, XGraphics gfx, XUnit xunitsBetweenTrimAndMediaBox)
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
						 XUnit.FromPoint(upperRightTrimBoxCorner.X + xunitsBetweenTrimAndMediaBox.Point).Point, upperLeftTrimBoxCorner.Y);
			gfx.DrawLine(pen, upperRightTrimBoxCorner.X, XUnit.FromPoint(upperRightTrimBoxCorner.Y - gapLength.Point).Point, upperRightTrimBoxCorner.X,
						 XUnit.FromPoint(upperLeftTrimBoxCorner.Y - xunitsBetweenTrimAndMediaBox.Point).Point);

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
