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
        public Vector3 PhotoPoint => transform.position+Vector3.up*1.25f;
        public int Floor => Mathf.Clamp(Mathf.FloorToInt((transform.position.y+.15f)/Session.World.Data.floorHeight),0,Session.World.Data.floors.Count-1);

        public void Initialize(MatchSession session,PlacedItemView computer)
        {
            Session=session; Computer=computer;
            transform.rotation=computer.transform.rotation;
            gameObject.layer=PrototypeLayers.Get("UA_Player");
            Model=new GameObject("StreamerVisual").transform; Model.SetParent(transform,false);
            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name="MonsterBody"; body.transform.SetParent(Model,false);
            body.transform.localPosition=Vector3.up; body.transform.localScale=new Vector3(.7f,1,.7f);
            body.layer=PrototypeLayers.Get("UA_Player"); body.GetComponent<Collider>().enabled=false;
            body.GetComponent<Renderer>().sharedMaterial=session.World.Palette.Material(new Color(.52f,.17f,.24f));
            session.World.Palette.Box(Model,"LeftHorn",new Vector3(-.23f,2.05f,0),new Vector3(.12f,.45f,.14f),new Color(.9f,.76f,.54f),"UA_Player",false);
            session.World.Palette.Box(Model,"RightHorn",new Vector3(.23f,2.05f,0),new Vector3(.12f,.45f,.14f),new Color(.9f,.76f,.54f),"UA_Player",false);
            var head=new GameObject("StreamerCamera"); head.transform.SetParent(transform,false); head.transform.localPosition=Vector3.up*1.7f;
            View=head.AddComponent<Camera>(); View.fieldOfView=65; View.nearClipPlane=.08f; View.farClipPlane=6.5f;
            View.clearFlags=CameraClearFlags.SolidColor; View.backgroundColor=Color.black;
            View.cullingMask=~LayerMask.GetMask("UA_Player");
            ViewTexture=new RenderTexture(480,270,24){name="StreamerPerception"}; ViewTexture.Create(); View.targetTexture=ViewTexture;
            Controller=gameObject.AddComponent<StreamerController>(); Controller.Initialize(this);
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
