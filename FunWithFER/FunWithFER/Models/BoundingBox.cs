using System.Drawing;
using CommonHelpers.Common;

namespace FunWithFER.Models
{
    public class BoundingBox : BindableBase
    {
        private string label;
        private float confidence;
        private float x;
        private float y;
        private float width;
        private float height;

        public string Label
        {
            get => label;
            set => SetProperty(ref label, value);
        }

        public float Confidence
        {
            get => confidence;
            set => SetProperty(ref confidence, value);
        }

        public float X
        {
            get => x;
            set
            {
                if (SetProperty(ref x, value))
                {
                    OnPropertyChanged(nameof(Rect));
                }
            }
        }

        public float Y
        {
            get => y;
            set
            {
                if (SetProperty(ref y, value))
                {
                    OnPropertyChanged(nameof(Rect));
                }
            }
        }

        public float Width
        {
            get => width;
            set
            {
                if (SetProperty(ref width, value))
                {
                    OnPropertyChanged(nameof(Rect));
                }
            }
        }

        public float Height
        {
            get => height;
            set
            {
                if (SetProperty(ref height, value))
                {
                    OnPropertyChanged(nameof(Rect));
                }
            }
        }

        public RectangleF Rect => new RectangleF(X, Y, Width, Height);
    }
}
