using UnityEngine;

namespace UnderAvatar
{
    public enum StreamerState { Roaming, Returning, Streaming, Turning, Ambush, Stunned }

    public sealed class StreamerActor : MonoBehaviour
    {
        public StreamerState State { get; internal set; } = StreamerState.Streaming;
        public float Progress { get; internal set; }
        public float AlertUntil { get; private set; }
        public Camera View { get; private set; }
        public RenderTexture ViewTexture { get; private set; }
        public PlacedItemView Computer { get; private set; }
        public MatchSession Session { get; private set; }
        public StreamerController Controller { get; private set; }
        public Transform Model { get; private set; }
        public CharacterAppearance Appearance { get; private set; }
        public VisionLight Vision { get; private set; }
        public Vector3 PhotoPoint => transform.position+Vector3.up*1.25f;
        public int Floor => Mathf.Clamp(Mathf.FloorToInt((transform.position.y+.15f)/Session.World.Data.floorHeight),0,Session.World.Data.floors.Count-1);

        public void Initialize(MatchSession session,PlacedItemView computer)
        {
            Session=session; Computer=computer;
            transform.rotation=computer.transform.rotation;
            gameObject.layer=PrototypeLayers.Get("UA_Player");
            Model=new GameObject("StreamerVisual").transform; Model.SetParent(transform,false);
            var head=new GameObject("StreamerCamera"); head.transform.SetParent(transform,false); head.transform.localPosition=Vector3.up*1.7f;
            View=head.AddComponent<Camera>(); View.fieldOfView=65; View.nearClipPlane=.08f; View.farClipPlane=150;
            View.clearFlags=CameraClearFlags.SolidColor; View.backgroundColor=Color.black;
            View.cullingMask=~LayerMask.GetMask("UA_Player");
            ViewTexture=new RenderTexture(480,270,24){name="StreamerPerception"}; ViewTexture.Create(); View.targetTexture=ViewTexture;
            Vision=VisionLight.Create(View,()=>session.Settings.streamerVision,"UA_Player");
            Controller=gameObject.AddComponent<StreamerController>(); Controller.Initialize(this);
            Appearance=Model.gameObject.AddComponent<CharacterAppearance>();Appearance.Initialize(session,TestRole.Streamer,Controller.CapturePose);
        }

        public void UsePlayerView()
        {
            View.targetTexture=null; View.depth=1;
            View.gameObject.AddComponent<AudioListener>();
        }
        public void OnPhotographed() { AlertUntil=Time.time+1.6f; }
        public void ApplyAlert(float remaining) { AlertUntil=Time.time+remaining; }
        void OnDestroy()
        {
            if(View) View.targetTexture=null;
            if(ViewTexture) { ViewTexture.Release(); PrototypeVisuals.Release(ViewTexture); }
        }
    }
}
