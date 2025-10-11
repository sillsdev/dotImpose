using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DotImpose.LayoutMethods
{
    /// <summary>
    /// Represents a target paper size for PDF output.
    /// </summary>
    public class PaperTarget
    {
        /// <summary>
        /// Gets or sets the name of the paper target (e.g., "A4", "Letter").
        /// </summary>
        public string Name;
        private XUnit _width;
        private XUnit _height;

        /// <summary>
        /// Initializes a new instance of the PaperTarget class.
        /// </summary>
        /// <param name="name">The name of the paper size.</param>
        /// <param name="pageSize">The PDF page size specification.</param>
        public PaperTarget(string name, PdfSharp.PageSize pageSize)
        {
            Name = name;
            PdfPage p = new PdfPage();
            p.Size = pageSize;
            _width = p.Width;
            _height = p.Height;


        }

        /// <summary>
        /// Gets the paper dimensions based on the input dimensions, adjusting for orientation.
        /// </summary>
        /// <param name="inputWidth">The width of the input PDF in pixels.</param>
        /// <param name="inputHeight">The height of the input PDF in pixels.</param>
        /// <returns>An XPoint containing the paper dimensions in points.</returns>
        public XPoint GetPaperDimensions(int inputWidth, int inputHeight)
        {
            if (inputHeight > inputWidth)
            {
                return new XPoint(_height.Point, _width.Point);//portrait
            }
            else  // Square is laid out on portrait 6up, so treat it the same as 4up landscape for printer paper orientation.
            {
                return new XPoint(_width.Point, _height.Point); //landscape
            }
        }

        /// <summary>
        /// Returns a string representation of the paper target.
        /// </summary>
        /// <returns>The name of the paper target.</returns>
        public override string ToString()
        {
            return Name;
        }
    }

    /*    class A4PaperTarget : PaperTarget
        {
            public A4PaperTarget()
                : base(StaticName, 0, 0)
            {

            }
            public override Point GetPaperDimensions(int inputWidth, int inputHeight)
            {
                //todo: this is a hack, because of these units games we're playing...

                var a4 = new PdfPage();
                a4.Size = PageSize.A4;

                if (inputHeight > inputWidth)
                {
                    return new Point((int) a4.Height, (int) a4.Width);//portrait
                }
                else
                {
                    return new Point((int)a4.Width, (int)a4.Height); //landscape
                }
            }

            public override string ToString()
            {
                return "A4";
            }
            public const string StaticName = @"A4";//this is tied to use settings, so don't change it.
        }

        class DoublePaperTarget : PaperTarget
        {
            public DoublePaperTarget()
                : base(StaticName, 0,0)
            {

            }
            public override Point GetPaperDimensions(int inputWidth, int inputHeight)
            {
                if (inputHeight > inputWidth)
                {
                    return new Point(inputWidth*2, inputHeight);//portrait
                }
                else
                {
                    return new Point(inputWidth, inputHeight*2); //landscape
                }
            }

            public override string ToString()
            {
                return "Same Size";
            }
            public const string StaticName = @"PreservePage";//this is tied to use settings, so don't change it.
        }

        class SameSizePaperTarget : PaperTarget
        {
            public SameSizePaperTarget()
                : base(StaticName, 0, 0)
            {

            }

            public const string StaticName = @"ShrinkPage";//this is tied to use settings, so don't change it.

            public override Point GetPaperDimensions(int inputWidth, int inputHeight)
            {
                return new Point(inputHeight, inputWidth);
            }

            public override string ToString()
            {
                return "Shrink To Fit";
            }
        }*/
}
