using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    public sealed partial class MatchSession
    {
        public bool StreamerMapOpen {get;private set;}
        public bool LocalPhoneOpen => IsStreamerPlayer?StreamerMapOpen:Reporter.PhoneOpen;
        public bool LocalPhoneMenuOpen => IsStreamerPlayer?StreamerMapOpen:PhoneMenuOpen;
        public void SetStreamerMap(bool open)
        {
            if(!IsStreamerPlayer || open && Finished) return;
            if(open && !IsTraining && Streamer.State==StreamerState.Streaming)
            {Notice("直播时不能打开地图，请通关下播后再查看。");return;}
            StreamerMapOpen=open;ActivePhonePage=PhonePage.Map;
            Reporter.SetCursor(open || DebugOpen || Finished);
        }
        bool UpdateLocalPhoneAndTab()
        {
            var keys=Keyboard.current;
            if(!Finished && keys?.tabKey.wasPressedThisFrame==true)
            {
                DebugOpen=!DebugOpen;
                if(DebugOpen) {if(IsStreamerPlayer) SetStreamerMap(false);else if(Reporter.PhoneOpen) QueuePhoneAction(close:true);}
                Reporter.SetCursor(DebugOpen || LocalPhoneMenuOpen);return true;
            }
            if(!IsStreamerPlayer) return false;
            if((Finished || Streamer.State==StreamerState.Streaming && !IsTraining) && StreamerMapOpen) SetStreamerMap(false);
            if(!Finished && !DebugOpen && (keys?.fKey.wasPressedThisFrame==true || keys?.mKey.wasPressedThisFrame==true))
            {SetStreamerMap(!StreamerMapOpen);return true;}
            if(StreamerMapOpen)
            {if(keys?.escapeKey.wasPressedThisFrame==true) SetStreamerMap(false);return true;}
            return false;
        }
    }
}
