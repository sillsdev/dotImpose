using System;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace DotImpose.LayoutMethods
{
	/// <summary>
	/// Pass the input PDF file along to the output while preserving source box intent.
	/// When a source page does not define an explicit TrimBox, this method can synthesize one
	/// by insetting from the source BleedBox/MediaBox using insetTrimboxMillimeters.
	/// </summary>
	public class NullLayoutMethod : LayoutMethod
	{
		/// <summary>
		/// Deprecated trim inset amount in millimeters.
		/// </summary>
		protected double _insetTrimboxMillimeters;
		private const double kBleedMicroDeltaMM = 0.01; // use for floating point comparisons instead of 0.0

		/// <summary>
		/// Initializes a new instance of the NullLayoutMethod class with no synthetic trim inset.
		/// </summary>
		public NullLayoutMethod() : base("original", "Original")
		{
			_insetTrimboxMillimeters = 0.0;
		}

		/// <summary>
		/// Initializes a new instance of the NullLayoutMethod class.
		/// </summary>
		/// <param name="insetTrimboxMillimeters">
		/// Deprecated: The amount in mm used to synthesize a TrimBox inset when the source page has no explicit TrimBox.
		/// This parameter is source-data-dependent and will be removed in a future release.
		/// If source pages define an explicit TrimBox, passing a non-zero insetTrimboxMillimeters now throws.
		/// </param>
		[Obsolete("insetTrimboxMillimeters is deprecated and will be removed in a future release. Prefer defining TrimBox/BleedBox in the source PDF.")]
		public NullLayoutMethod(double insetTrimboxMillimeters) : base("original", "Original")
		{
			_insetTrimboxMillimeters = insetTrimboxMillimeters;
		}

		/// <summary>
		/// Performs the layout operation, optionally adding bleed margins and crop marks.
		/// </summary>
		public override void Layout(XPdfForm inputPdf, string inputPath, string outputPath, PaperTarget paperTarget, bool rightToLeft, bool showCropMarks)
		{
			if (!showCropMarks && Math.Abs(_insetTrimboxMillimeters) < kBleedMicroDeltaMM)
			{
				File.Copy(inputPath, outputPath, true); // we don't have any value to add, so just deliver a copy of the original
			}
			else
			{
				//_rightToLeft = rightToLeft;
				_inputPdf = inputPdf;
				_showCropMarks = showCropMarks;
				EnsureSourcePageBoxesLoaded(inputPath);
				ThrowIfDeprecatedInsetTrimboxMillimetersUsedWithExplicitSourceTrim();

				PdfDocument outputDocument = new PdfDocument();
				outputDocument.PageLayout = PdfPageLayout.SinglePage;

				// Despite the name, PixelWidth is the same as PointWidth, just as an integer instead of
				// double precision.  We may as well use all the precision we can get.
				_paperWidth = XUnit.FromPoint(_inputPdf.PointWidth);
				_paperHeight = XUnit.FromPoint(_inputPdf.PointHeight);

				// NB: Setting outputDocument.Settings.TrimMargins.All does not do what we want: it either
				// shrinks or expands the MediaBox/CropBox/BleedBox sizes.  It does not change either
				// TrimBox or ArtBox.

				for (int idx = 1; idx <= _inputPdf.PageCount; idx++)
				{
					using (XGraphics gfx = GetGraphicsForNullPage(outputDocument, idx))
					{
						DrawPage(gfx, idx);
					}
				}

				var tempPath = Path.ChangeExtension(Path.Combine(Path.GetDirectoryName(outputPath),
					Path.GetRandomFileName()), "pdf");
				outputDocument.Save(tempPath);
				outputDocument.Close();
				outputDocument = PdfReader.Open(tempPath, PdfDocumentOpenMode.Import);

				var cropMarkMargin = _showCropMarks
					? XUnit.FromMillimeter(kMillimetersBetweenTrimAndMediaBox)
					: XUnit.FromPoint(0);

				var pageIndex = 1;

				PdfDocument realOutput = new PdfDocument();
				realOutput.PageLayout = PdfPageLayout.SinglePage;
				foreach (var page in outputDocument.Pages)
				{
					GetFinalBoxRectangles(pageIndex, cropMarkMargin, out var trimBoxRect, out var bleedBoxRect);
					var trimBox = ToPdfRectangle(trimBoxRect);
					var bleedBox = ToPdfRectangle(bleedBoxRect);

					// Set CropBox the same as MediaBox.  CropBox limits what you see in Adobe Acrobat Reader DC and even Acrobat Pro.
					page.BleedBox = bleedBox;
					page.CropBox = page.MediaBox;
					page.ArtBox = trimBox;
					page.TrimBox = trimBox;
					realOutput.AddPage(page);
					pageIndex++;
				}
				outputDocument.Close();
				File.Delete(tempPath);
				realOutput.Save(outputPath);
			}
		}

		private void ThrowIfDeprecatedInsetTrimboxMillimetersUsedWithExplicitSourceTrim()
		{
			if (Math.Abs(_insetTrimboxMillimeters) < kBleedMicroDeltaMM)
				return;

			for (var pageNumber = 1; pageNumber <= _inputPdf.PageCount; pageNumber++)
			{
				if (GetSourcePageBoxes(pageNumber).HasExplicitTrimBox)
				{
					throw new InvalidOperationException(
						"NullLayoutMethod(insetTrimboxMillimeters) is deprecated and cannot be used when the source PDF defines TrimBox. " +
						"Use NullLayoutMethod() and define trim/bleed boxes in the source PDF.");
				}
			}
		}

		private XGraphics GetGraphicsForNullPage(PdfDocument outputDocument, int pageNumber)
		{
			if (!_showCropMarks)
				return GetGraphicsForNewPage(outputDocument);

			var page = outputDocument.AddPage();
			var cropMarkMargin = XUnit.FromMillimeter(kMillimetersBetweenTrimAndMediaBox);
			page.Width = XUnit.FromPoint(_paperWidth.Point + 2 * cropMarkMargin.Point);
			page.Height = XUnit.FromPoint(_paperHeight.Point + 2 * cropMarkMargin.Point);

			GetFinalBoxRectangles(pageNumber, cropMarkMargin, out var trimBoxRect, out var bleedBoxRect);
			page.TrimBox = ToPdfRectangle(trimBoxRect);
			page.BleedBox = ToPdfRectangle(bleedBoxRect);
			page.CropBox = page.MediaBox;

			var gfx = XGraphics.FromPdfPage(page);
			DrawCropMarks(page, gfx, cropMarkMargin);
			gfx.TranslateTransform(cropMarkMargin.Point, cropMarkMargin.Point);
			return gfx;
		}

		private void GetFinalBoxRectangles(int pageNumber, XUnit cropMarkMargin, out XRect trimBoxRect, out XRect bleedBoxRect)
		{
			var sourceBoxes = GetSourcePageBoxes(pageNumber);
			trimBoxRect = sourceBoxes.TrimBox;
			bleedBoxRect = sourceBoxes.BleedBox;

			// Preserve explicit source trim intent; insetTrimboxMillimeters is only used to synthesize trim when no trim box exists.
			if (Math.Abs(_insetTrimboxMillimeters) > kBleedMicroDeltaMM && !sourceBoxes.HasExplicitTrimBox)
			{
				var bleedInset = XUnit.FromMillimeter(_insetTrimboxMillimeters);
				trimBoxRect = InsetBox(bleedBoxRect, bleedInset.Point);
			}

			if (cropMarkMargin.Point > 0)
			{
				trimBoxRect = OffsetBox(trimBoxRect, cropMarkMargin.Point, cropMarkMargin.Point);
				bleedBoxRect = OffsetBox(bleedBoxRect, cropMarkMargin.Point, cropMarkMargin.Point);
			}
		}

		/// <summary>
		/// Not implemented for NullLayoutMethod as it uses a different layout approach.
		/// </summary>
		protected override void LayoutInner(PdfDocument outputDocument, int numberOfSheetsOfPaper, int numberOfPageSlotsAvailable, int vacats)
		{
			throw new NotImplementedException();
		}

		private void DrawPage(XGraphics targetGraphicsPort, int pageNumber)
		{
			_inputPdf.PageNumber = pageNumber;

			XRect sourceRect = new XRect(0, 0, _inputPdf.PixelWidth, _inputPdf.PixelHeight);
			//what's not obvious here is that the targetGraphicsPort has previously been
			//set up to do a transformation to shift the content down and to the right into its trimbox
			targetGraphicsPort.DrawImage(_inputPdf, sourceRect);
		}

		/// <summary>
		/// Determines whether this layout method is enabled. Always returns true for NullLayoutMethod.
		/// </summary>
		public override bool GetIsEnabled(XPdfForm inputPdf)
		{
			return true;
		}

		/// <summary>
		/// Gets a value indicating that this layout method is sensitive to page orientation.
		/// </summary>
		public override bool ImageIsSensitiveToOrientation
		{
			get { return true; }
		}

	}
}
