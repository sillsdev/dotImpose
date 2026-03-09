using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;

namespace DotImpose.LayoutMethods
{
	/// <summary>
	/// Layout a 4up booklet that will be folded horizontally and cut vertically.  This may be
	/// either portrait or landscape in orientation depending on the original page layout.
	/// </summary>
	public class SideFold4UpBookletLayouter : LayoutMethod
	{
		private XRect _upperLeftTrimBox;
		private XRect _upperRightTrimBox;
		private XRect _lowerLeftTrimBox;
		private XRect _lowerRightTrimBox;
		private XRect _sheetTrimBox;
		private double _horizontalCutUpperY;
		private double _horizontalCutLowerY;

		/// <summary>
		/// Initializes a new instance of the SideFold4UpBookletLayouter class.
		/// </summary>
		public SideFold4UpBookletLayouter() : base("sideFoldCut4UpBooklet", "Fold/Cut 4Up Booklet")
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
		/// Performs the inner layout logic for 4-up side fold booklet layout.
		/// </summary>
		protected override void LayoutInner(PdfDocument outputDocument, int numberOfSheetsOfPaper, int numberOfPageSlotsAvailable, int vacats)
		{
			for (var idx = 1; idx <= numberOfSheetsOfPaper; idx++)
			{
				XGraphics gfx;
				// Front page of a sheet:
				using (gfx = GetGraphicsForNewPage(outputDocument))
				{
					//Left side of front
					if (vacats > 0) // Skip if left side has to remain blank
						vacats -= 1;
					else
						DrawSuperiorSide(gfx, numberOfPageSlotsAvailable + 2 * (1 - idx));

					//Right side of the front
					DrawInferiorSide(gfx, 2 * idx - 1);

					if (_showCropMarks)
					{
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutUpperY);
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutLowerY);
					}
				}

				// Back page of a sheet
				using (gfx = GetGraphicsForNewPage(outputDocument))
				{
					if (2 * idx <= _inputPdf.PageCount) //prevent asking for page 2 with a single page document (JH Oct 2010)
														//Left side of back
						DrawSuperiorSide(gfx, 2 * idx);

					//Right side of the Back
					if (vacats > 0) // Skip if right side has to remain blank
						vacats -= 1;
					else
						DrawInferiorSide(gfx, numberOfPageSlotsAvailable + 1 - 2 * idx);

					if (_showCropMarks)
					{
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutUpperY);
						DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, _horizontalCutLowerY);
					}
				}
			}
		}

		private void DrawInferiorSide(XGraphics gfx, int pageNumber)
		{
			var upperBox = _rightToLeft ? _upperLeftTrimBox : _upperRightTrimBox;
			var lowerBox = _rightToLeft ? _lowerLeftTrimBox : _lowerRightTrimBox;
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, upperBox);
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, lowerBox);
		}

		private void DrawSuperiorSide(XGraphics gfx, int pageNumber)
		{
			var upperBox = _rightToLeft ? _upperRightTrimBox : _upperLeftTrimBox;
			var lowerBox = _rightToLeft ? _lowerRightTrimBox : _lowerLeftTrimBox;
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, upperBox);
			DrawPageUsingSourceTrimIntent(gfx, pageNumber, lowerBox);
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
