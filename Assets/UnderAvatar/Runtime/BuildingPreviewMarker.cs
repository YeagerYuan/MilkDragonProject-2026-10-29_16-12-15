#if UNITY_EDITOR
using UnityEngine;
namespace UnderAvatar.Editor
{
    // Transient Scene-view objects only; never serialized into a scene or build.
    public sealed class BuildingPreviewMarker : MonoBehaviour { }
}
#endif
