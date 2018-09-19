using System.Drawing;

namespace FunWithFER.Effects.Models
{
    internal class BoundingBox
    {
        public string Label { get; set; }
        public float Confidence { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        public float Width { get; set; }

        public float Height { get; set; }


        public RectangleF Rect => new RectangleF(X, Y, Width, Height);
    }
}
