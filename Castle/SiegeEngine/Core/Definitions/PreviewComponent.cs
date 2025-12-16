using System;

namespace SiegeEngine.Core.Definitions
{
    public class PreviewComponent : IComponent
    {
        public ulong PlayerId { get; set; }
        public bool IsPreview { get; set; }
        public string BrushType { get; set; }

        public PreviewComponent(ulong playerId, string brushType)
        {
            PlayerId = playerId;
            IsPreview = true;
            BrushType = brushType;
        }
    }
}