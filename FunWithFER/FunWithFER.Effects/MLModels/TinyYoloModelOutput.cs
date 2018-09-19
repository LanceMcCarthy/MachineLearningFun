using System.Collections.Generic;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
namespace FunWithFER.Effects.MLModels
{
    public sealed class TinyYoloModelOutput
    {
        public IList<float> grid { get; set; }

        public TinyYoloModelOutput()
        {
            grid = new List<float>();
        }
    }
}