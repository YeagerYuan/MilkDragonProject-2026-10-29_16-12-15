using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    [InitializeOnLoad]
    public static class PrototypePlayView
    {
        const string RestoreKey="UnderAvatar.RestoreGameView";
        const string PreviousKey="UnderAvatar.PreviousGameViewMaximized";
        public static bool ExpandOnPlay
        {
            get => EditorPrefs.GetBool("UnderAvatar.ExpandTestGameView",true);
            set => EditorPrefs.SetBool("UnderAvatar.ExpandTestGameView",value);
        }
        static PrototypePlayView() { EditorApplication.playModeStateChanged+=OnPlayMode; }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode && ExpandOnPlay &&
                Object.FindObjectsByType<PrototypeApp>(FindObjectsSortMode.None).Any(a=>a.gameObject.scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
            {
                var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                SessionState.SetBool(RestoreKey,true); SessionState.SetBool(PreviousKey,view.maximized);
                view.maximized=true; view.Show(); view.Focus();
            }
            else if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RestoreKey,false))
            {
                SessionState.EraseBool(RestoreKey);
                var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                view.maximized=SessionState.GetBool(PreviousKey,false);
                if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Focus();
            }
        }
    }
}
