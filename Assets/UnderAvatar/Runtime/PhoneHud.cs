using UnityEngine;
namespace UnderAvatar
{
    public sealed partial class PrototypeHud
    {
        PaparazziOverlay phoneOverlay;
        void DrawPhone(MatchSession match)
        {
            if(!phoneOverlay) phoneOverlay=GetComponent<PaparazziOverlay>()??gameObject.AddComponent<PaparazziOverlay>();
            phoneOverlay.Draw(match);
        }
    }
}
