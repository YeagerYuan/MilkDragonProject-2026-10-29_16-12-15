using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    public sealed class DisplayModeSettings : MonoBehaviour
    {
        const string Preference="UnderAvatar.FullScreen";
        public bool FullScreen { get; private set; }
        public Vector2Int RenderResolution { get; private set; }
        void Awake() => RenderResolution=new Vector2Int(Screen.width,Screen.height);
        void Start() => SetFullScreen(PlayerPrefs.GetInt(Preference,1)!=0,false);
        public void SetFullScreen(bool enabled,bool save=true)
        {
            FullScreen=enabled;
#if !UNITY_EDITOR
            // Keep the requested render dimensions; borderless mode scales them to the desktop.
            Screen.SetResolution(RenderResolution.x,RenderResolution.y,enabled?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
#endif
            if(save) {PlayerPrefs.SetInt(Preference,enabled?1:0);PlayerPrefs.Save();}
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null && (keyboard.f11Key.wasPressedThisFrame || keyboard.altKey.isPressed && keyboard.enterKey.wasPressedThisFrame))
                SetFullScreen(!FullScreen);
        }
    }
}
