using System.Diagnostics;
using System;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DotImpose.LayoutMethods
{
	/// <summary>
	/// Layout a 4up booklet that will be folded horizontally and cut vertically.  This may be
	/// either portrait or landscape in orientation depending on the original page layout.
	/// </summary>
	public class SideFold4UpSingleBookletLayouter : LayoutMethod
	{
		private XRect _upperLeftTrimBox;
		private XRect _upperRightTrimBox;
		private XRect _lowerLeftTrimBox;
		private XRect _lowerRightTrimBox;
		private XRect _sheetTrimBox;
		private double _horizontalCutUpperY;
		private double _horizontalCutLowerY;

		/// <summary>
		/// Initializes a new instance of the SideFold4UpSingleBookletLayouter class.
		/// </summary>
		public SideFold4UpSingleBookletLayouter() : base("sideFoldCut4UpSingleBooklet", "Fold/Cut Single 4Up Booklet")
		{

		}

		/// <summary>
		/// 4up layout requires matching paper orientation instead of the opposite paper orientation.
		/// This method achieves that happy state.
		/// </summary>
		protected override void SetPaperSize(PaperTarget paperTarget)
		{
			var size = paperTarget.GetPaperDimensions(_inputPdf.PixelHeight, _inputPdf.PixelWidth);
			_paperWidth = XUnit.FromPoint(size.X);
			_paperHeight = XUnit.FromPoint(size.Y);
			InitializePanelGeometry();
		}

		/// <summary>
		/// Gets the sheet-level trim box that spans from the upper-left panel trim origin
		/// to the lower-right panel trim extent.
		/// </summary>
		protected override XRect GetSheetTrimBoxInPaperCoordinates()
		{
			return _sheetTrimBox.Width > 0 && _sheetTrimBox.Height > 0
				? _sheetTrimBox
				: base.GetSheetTrimBoxInPaperCoordinates();
		}

		/// <summary>
		/// Performs the inner layout logic for single 4-up side fold booklet layout.
		/// </summary>
		protected override void LayoutInner(PdfDocument outputDocument, int numberOfSheetsOfPaper, int numberOfPageSlotsAvailable, int vacats)
		{
			// Recalculate for showing 4up instead of 2up on each side of a sheet.
			// This layout minimizes the use of paper for a single booklet.
			int inputPages = _inputPdf.PageCount;
			numberOfSheetsOfPaper = inputPages / 8;
			if (numberOfSheetsOfPaper * 8 < inputPages)
				numberOfSheetsOfPaper += 1;
			numberOfPageSlotsAvailable = 8 * numberOfSheetsOfPaper;
			vacats = numberOfPageSlotsAvailable - inputPages;
			Debug.Assert(vacats >= 0 && vacats < 8);
			int vacatsSkipped = 0;
			bool skipLastRow = vacats >= 4;

			XGraphics gfx;
			for (int idx = 1; idx <= numberOfSheetsOfPaper; idx++)
			{
				bool onFirstSheet = idx == 1;
				bool onLastSheet = idx == numberOfSheetsOfPaper;

				// Front side of a sheet:
				int topLeftFrontPage = numberOfPageSlotsAvailable - (4 * (idx - 1));
				if (skipLastRow)
					topLeftFrontPage -= 4;
				int bottomLeftFrontPage = topLeftFrontPage - 2;
				int topRightFrontPage = 4 * idx - 3;
				int bottomRightFrontPage = topRightFrontPage + 2;
				using (gfx = GetGraphicsForNewPage(outputDocument))
				{
					if (onFirstSheet && topLeftFrontPage > inputPages)
						++vacatsSkipped;
					else
						DrawTopLeftCorner(gfx, topLeftFrontPage);

					if ((onFirstSheet && bottomLeftFrontPage > inputPages) || (onLastSheet && skipLastRow))
						++vacatsSkipped;
					else
						DrawBottomLeftCorner(gfx, bottomLeftFrontPage);

					DrawTopRightCorner(gfx, topRightFrontPage);

					if ((onFirstSheet && bottomRightFrontPage > inputPages) || (onLastSheet && skipLastRow))
						++vacatsSkipped;
					else
						DrawBottomRightCorner(gfx, bottomRightFrontPage);

					if (_showCropMarks)
					{
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutUpperY);
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutLowerY);
					}
				}

				// Back side of a sheet:
				int topLeftBackPage = topRightFrontPage + 1;
				int bottomLeftBackPage = bottomRightFrontPage + 1;
				int topRightBackPage = topLeftFrontPage - 1;
				int bottomRightBackPage = bottomLeftFrontPage - 1;
				using (gfx = GetGraphicsForNewPage(outputDocument))
				{
					if (topLeftBackPage > inputPages)
						++vacatsSkipped;
					else
						DrawTopLeftCorner(gfx, topLeftBackPage);

					if ((onFirstSheet && bottomLeftBackPage > inputPages) || (onLastSheet && skipLastRow))
						++vacatsSkipped;
					else
						DrawBottomLeftCorner(gfx, bottomLeftBackPage);

					if (onFirstSheet && topRightBackPage > inputPages)
						++vacatsSkipped;
					else
						DrawTopRightCorner(gfx, topRightBackPage);

					if ((onFirstSheet && bottomRightBackPage > inputPages) || (onLastSheet && skipLastRow))
						++vacatsSkipped;
					else
						DrawBottomRightCorner(gfx, bottomRightBackPage);

					if (_showCropMarks)
					{
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutUpperY);
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutLowerY);
					}
				}
			}
			Debug.Assert(vacats == vacatsSkipped);
		}

		/// <summary>
		/// Performs the inner layout logic for single 4-up side fold booklet layout.
		/// </summary>
		private void DrawTopLeftCorner(XGraphics gfx, int pageNumber /* NB: page number is one-based*/)
		{
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, _rightToLeft ? _upperRightTrimBox : _upperLeftTrimBox);
		}

		private void DrawBottomLeftCorner(XGraphics gfx, int pageNumber /* NB: page number is one-based*/)
		{
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, _rightToLeft ? _lowerRightTrimBox : _lowerLeftTrimBox);
		}

		private void DrawTopRightCorner(XGraphics gfx, int pageNumber /* NB: page number is one-based*/)
		{
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, _rightToLeft ? _upperLeftTrimBox : _upperRightTrimBox);
		}

		private void DrawBottomRightCorner(XGraphics gfx, int pageNumber /* NB: page number is one-based*/)
		{
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, _rightToLeft ? _lowerLeftTrimBox : _lowerRightTrimBox);
		}

		private void InitializePanelGeometry()
		{
			var sourceBoxes = GetSourcePageBoxes(1);
			var trim = sourceBoxes.TrimBox;
			if (trim.Width <= 0 || trim.Height <= 0)
				trim = new XRect(0, 0, _inputPdf.PointWidth, _inputPdf.PointHeight);

			var bleed = sourceBoxes.BleedBox;
			if (bleed.Width <= 0 || bleed.Height <= 0)
				bleed = trim;

			var sourceBleedLeft = Math.Max(0, trim.Left - bleed.Left);
			var sourceBleedRight = Math.Max(0, bleed.Right - trim.Right);
			var sourceBleedTop = Math.Max(0, trim.Top - bleed.Top);
			var sourceBleedBottom = Math.Max(0, bleed.Bottom - trim.Bottom);

			var sourceBleedWidth = trim.Width + sourceBleedLeft + sourceBleedRight;
			var sourceBleedHeight = trim.Height + sourceBleedTop + sourceBleedBottom;

			if (sourceBleedWidth <= 0 || sourceBleedHeight <= 0)
			{
				_sheetTrimBox = new XRect(0, 0, _paperWidth.Point, _paperHeight.Point);
				_upperLeftTrimBox = _sheetTrimBox;
				_upperRightTrimBox = _sheetTrimBox;
				_lowerLeftTrimBox = _sheetTrimBox;
				_lowerRightTrimBox = _sheetTrimBox;
				_horizontalCutUpperY = _sheetTrimBox.Y + (_sheetTrimBox.Height / 2);
				_horizontalCutLowerY = _horizontalCutUpperY;
				return;
			}

			var fitScale = Math.Min(_paperWidth.Point / (2 * sourceBleedWidth), _paperHeight.Point / (2 * sourceBleedHeight));
			var scale = Math.Min(1.0, fitScale);
			if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
				scale = 1.0;

			var bleedLeft = sourceBleedLeft * scale;
			var bleedTop = sourceBleedTop * scale;
			var trimWidth = trim.Width * scale;
			var trimHeight = trim.Height * scale;
			var bleedWidth = sourceBleedWidth * scale;
			var bleedHeight = sourceBleedHeight * scale;

			var gridOriginX = (_paperWidth.Point - (2 * bleedWidth)) / 2;
			var gridOriginY = (_paperHeight.Point - (2 * bleedHeight)) / 2;

			_upperLeftTrimBox = new XRect(gridOriginX + bleedLeft, gridOriginY + bleedTop, trimWidth, trimHeight);
			_upperRightTrimBox = new XRect(gridOriginX + bleedWidth + bleedLeft, gridOriginY + bleedTop, trimWidth, trimHeight);
			_lowerLeftTrimBox = new XRect(gridOriginX + bleedLeft, gridOriginY + bleedHeight + bleedTop, trimWidth, trimHeight);
			_lowerRightTrimBox = new XRect(gridOriginX + bleedWidth + bleedLeft, gridOriginY + bleedHeight + bleedTop, trimWidth, trimHeight);

			_sheetTrimBox = new XRect(
				_upperLeftTrimBox.Left,
				_upperLeftTrimBox.Top,
				_lowerRightTrimBox.Right - _upperLeftTrimBox.Left,
				_lowerRightTrimBox.Bottom - _upperLeftTrimBox.Top);

			_horizontalCutUpperY = _upperLeftTrimBox.Bottom;
			_horizontalCutLowerY = _lowerLeftTrimBox.Top;
		}

		/// <summary>
		/// Determines whether this layout method is enabled for the given input PDF. Enabled for both portrait and landscape orientations.
		/// </summary>
		public override bool GetIsEnabled(XPdfForm inputPdf)
		{
			return IsPortrait(inputPdf) || IsLandscape(inputPdf);
		}
	}
}
