using System;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DotImpose.LayoutMethods
{
    /// <summary>
    /// Layout a 6up square booklet that will be folded vertically and cut horizontally.  The actual
    /// paper will be treated as portrait to fit the 6 square pages.  (The actual pages are laid out
    /// centered both vertically and horizontally inside the A3 page, which produces 15mm margins top
    /// and bottom and 18.5mm margins left and right.)
    /// </summary>
    /// <remarks>
    /// Similarly to how the SideFold4UpBookletLayouter produces 2 copies of the booklet, this
    /// layout method produces 3 copies of the booklet.  It assumes printing on A3 paper.
    /// </remarks>
    public class Square6UpBookletLayouter : LayoutMethod
    {
        private XRect[] _leftColumnTrimBoxes = new XRect[3];
        private XRect[] _rightColumnTrimBoxes = new XRect[3];
        private XRect _sheetTrimBox;
        private double[] _horizontalCutGuideYs = Array.Empty<double>();

        /// <summary>
        /// Initializes a new instance of the Square6UpBookletLayouter class.
        /// </summary>
        public Square6UpBookletLayouter() : base("square6UpBooklet", "Fold/Cut 6Up Square Booklet")
        {

        }

        /// <summary>
        /// 6up layout requires portrait orientation of the paper.
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
        /// Gets the sheet-level trim box spanning the union of the 2x3 panel trim boxes.
        /// </summary>
        protected override XRect GetSheetTrimBoxInPaperCoordinates()
        {
            return _sheetTrimBox.Width > 0 && _sheetTrimBox.Height > 0
                ? _sheetTrimBox
                : base.GetSheetTrimBoxInPaperCoordinates();
        }

        private double GetMinimumSourceTrimSize()
        {
            var minTrimSize = double.MaxValue;
            for (var pageNumber = 1; pageNumber <= _inputPdf.PageCount; pageNumber++)
            {
                var trimBox = GetSourcePageBoxes(pageNumber).TrimBox;
                var trimSize = Math.Min(trimBox.Width, trimBox.Height);
                if (trimSize > 0)
                    minTrimSize = Math.Min(minTrimSize, trimSize);
            }

            return minTrimSize == double.MaxValue ? 0 : minTrimSize;
        }

        /// <summary>
        /// Performs the inner layout logic for 6-up square booklet layout.
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
                        DrawSideCutGuides(gfx);
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
                        DrawSideCutGuides(gfx);
                }
            }
        }

        private void DrawInferiorSide(XGraphics gfx, int pageNumber)
        {
            DrawSide(gfx, pageNumber, _rightToLeft ? _leftColumnTrimBoxes : _rightColumnTrimBoxes);
        }

        private void DrawSuperiorSide(XGraphics gfx, int pageNumber)
        {
            DrawSide(gfx, pageNumber, _rightToLeft ? _rightColumnTrimBoxes : _leftColumnTrimBoxes);
        }

        private void DrawSide(XGraphics gfx, int pageNumber, XRect[] trimBoxes)
        {
            if (trimBoxes == null || trimBoxes.Length == 0)
                return;

            for (var row = 0; row < trimBoxes.Length; row++)
            {
                DrawPageUsingSourceTrimIntent(gfx, pageNumber, trimBoxes[row]);
            }
        }

        private void DrawSideCutGuides(XGraphics gfx)
        {
            if (_horizontalCutGuideYs == null || _horizontalCutGuideYs.Length == 0)
                return;

            foreach (var y in _horizontalCutGuideYs)
                DrawCenterCutGuideSegments(gfx, _sheetTrimBox, 0, y);
        }

        private void InitializePanelGeometry()
        {
            _leftColumnTrimBoxes = new XRect[3];
            _rightColumnTrimBoxes = new XRect[3];
            _horizontalCutGuideYs = Array.Empty<double>();

            var sourceBoxes = GetSourcePageBoxes(1);
            var sourceTrim = sourceBoxes.TrimBox;
            if (sourceTrim.Width <= 0 || sourceTrim.Height <= 0)
                sourceTrim = new XRect(0, 0, _inputPdf.PointWidth, _inputPdf.PointHeight);

            var sourceBleed = sourceBoxes.BleedBox;
            if (sourceBleed.Width <= 0 || sourceBleed.Height <= 0)
                sourceBleed = sourceTrim;

            var sourceTrimSize = GetMinimumSourceTrimSize();
            if (sourceTrimSize <= 0)
                sourceTrimSize = Math.Min(sourceTrim.Width, sourceTrim.Height);
            if (sourceTrimSize <= 0)
                sourceTrimSize = Math.Min(_inputPdf.PointWidth, _inputPdf.PointHeight);

            var panelTrimSize = sourceTrimSize;
            var scaleXToTrim = panelTrimSize / sourceTrim.Width;
            var scaleYToTrim = panelTrimSize / sourceTrim.Height;

            var sourceBleedLeft = Math.Max(0, sourceTrim.Left - sourceBleed.Left) * scaleXToTrim;
            var sourceBleedRight = Math.Max(0, sourceBleed.Right - sourceTrim.Right) * scaleXToTrim;
            var sourceBleedTop = Math.Max(0, sourceTrim.Top - sourceBleed.Top) * scaleYToTrim;
            var sourceBleedBottom = Math.Max(0, sourceBleed.Bottom - sourceTrim.Bottom) * scaleYToTrim;

            var sourceBleedWidth = panelTrimSize + sourceBleedLeft + sourceBleedRight;
            var sourceBleedHeight = panelTrimSize + sourceBleedTop + sourceBleedBottom;

            if (sourceBleedWidth <= 0 || sourceBleedHeight <= 0)
            {
                _sheetTrimBox = new XRect(0, 0, _paperWidth.Point, _paperHeight.Point);
                for (var row = 0; row < 3; row++)
                {
                    _leftColumnTrimBoxes[row] = _sheetTrimBox;
                    _rightColumnTrimBoxes[row] = _sheetTrimBox;
                }
                return;
            }

            var fitScale = Math.Min(_paperWidth.Point / (2 * sourceBleedWidth), _paperHeight.Point / (3 * sourceBleedHeight));
            var scale = Math.Min(1.0, fitScale);
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                scale = 1.0;

            var bleedLeft = sourceBleedLeft * scale;
            var bleedTop = sourceBleedTop * scale;
            var trimSize = panelTrimSize * scale;
            var bleedWidth = sourceBleedWidth * scale;
            var bleedHeight = sourceBleedHeight * scale;

            var gridOriginX = (_paperWidth.Point - (2 * bleedWidth)) / 2;
            var gridOriginY = (_paperHeight.Point - (3 * bleedHeight)) / 2;

            for (var row = 0; row < 3; row++)
            {
                var y = gridOriginY + (row * bleedHeight) + bleedTop;
                _leftColumnTrimBoxes[row] = new XRect(gridOriginX + bleedLeft, y, trimSize, trimSize);
                _rightColumnTrimBoxes[row] = new XRect(gridOriginX + bleedWidth + bleedLeft, y, trimSize, trimSize);
            }

            _sheetTrimBox = new XRect(
                _leftColumnTrimBoxes[0].Left,
                _leftColumnTrimBoxes[0].Top,
                _rightColumnTrimBoxes[2].Right - _leftColumnTrimBoxes[0].Left,
                _rightColumnTrimBoxes[2].Bottom - _leftColumnTrimBoxes[0].Top);

            _horizontalCutGuideYs = new[]
            {
                _leftColumnTrimBoxes[0].Bottom,
                _leftColumnTrimBoxes[1].Top,
                _leftColumnTrimBoxes[1].Bottom,
                _leftColumnTrimBoxes[2].Top
            };
        }

        /// <summary>
        /// Determines whether this layout method is enabled for the given input PDF. Enabled only for square pages.
        /// </summary>
        public override bool GetIsEnabled(XPdfForm inputPdf)
        {
            // Available only for square input pages.
            return IsSquare(inputPdf);
        }
    }
}
