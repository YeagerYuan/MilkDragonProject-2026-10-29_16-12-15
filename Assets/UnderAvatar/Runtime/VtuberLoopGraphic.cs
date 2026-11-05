using UnityEngine;
using UnityEngine.UI;

namespace UnderAvatar
{
    /// <summary>A transparent, continuously deformed UI puppet with a deterministic seamless loop.</summary>
    [AddComponentMenu("Under Avatar/VTuber Loop")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VtuberLoopGraphic : MaskableGraphic
    {
        [Header("Artwork")]
        [SerializeField] Texture2D expressionAtlas;
        [Header("Loop")]
        [Min(4)] public float loopSeconds=12;
        [Range(0,2)] public float motionStrength=1;
        [Range(0,1)] public float talkingAmount=.7f;
        public bool animate=true;
        [Tooltip("Use unscaled time so the avatar remains smooth when the game time scale changes.")]
        public bool useUnscaledTime=true;
        Material animatedMaterial;
        Material sourceMaterial;
        double started;
        float phase,blink,talk;
        public override Texture mainTexture => expressionAtlas?expressionAtlas:Texture2D.whiteTexture;
        public void SetArtwork(Texture2D atlas) {expressionAtlas=atlas;SetAllDirty();}
        protected override void OnEnable()
        {
            base.OnEnable();started=Clock;
            if(Application.isPlaying && material && material.shader.name=="UnderAvatar/VTuberLoop")
            {
                sourceMaterial=material;
                animatedMaterial=new Material(material){name="VTuber Loop (instance)",hideFlags=HideFlags.DontSave};
                material=animatedMaterial;
            }
            raycastTarget=false;UpdateLoop();
        }
        double Clock => useUnscaledTime?Time.unscaledTimeAsDouble:Time.timeAsDouble;
        void Update() {if(Application.isPlaying && animate) UpdateLoop();}
        void UpdateLoop()
        {
            float seconds=Mathf.Max(4,loopSeconds);
            float time=(float)((Clock-started)%seconds);
            phase=time/seconds*Mathf.PI*2;
            blink=Mathf.Max(Blink(time,seconds*.225f,seconds),Blink(time,seconds*.565f,seconds),Blink(time,seconds*.85f,seconds));
            float speech=Mathf.Clamp01(.47f+.29f*Mathf.Sin(phase*29)+.18f*Mathf.Sin(phase*43+.5f)+.12f*Mathf.Sin(phase*17));
            float pause=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.4f,.4f,Mathf.Sin(phase*3-.5f)));
            talk=speech*pause*talkingAmount;
            if(animatedMaterial)
            {
                animatedMaterial.SetFloat("_Blink",blink);animatedMaterial.SetFloat("_Talk",talk);
                var rendered=materialForRendering;
                if(rendered && rendered!=animatedMaterial) {rendered.SetFloat("_Blink",blink);rendered.SetFloat("_Talk",talk);}
            }
            SetVerticesDirty();
        }
        static float Blink(float time,float center,float duration)
        {
            float distance=Mathf.Abs(time-center);distance=Mathf.Min(distance,duration-distance);
            // Smooth closing/opening rather than replacing discrete frames.
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.115f,distance));
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();
            const int columns=30,rows=40;
            float breath=Mathf.Sin(phase*3),turn=Mathf.Sin(phase),nod=Mathf.Sin(phase*2+.4f),hair=Mathf.Sin(phase*5-.8f);
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
            {
                float u=x/(float)columns,v=y/(float)rows;
                float head=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.29f,.46f,v));
                float fringe=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,.97f,v));
                float px=(u-.5f)*rect.width,py=(v-.5f)*rect.height;
                px+=motionStrength*(turn*rect.width*.012f*head+hair*rect.width*.0035f*fringe*Mathf.Sin(u*Mathf.PI));
                px*=1+motionStrength*breath*.004f*(1-head);
                py+=motionStrength*(breath*rect.height*.003f*v+nod*rect.height*.004f*head);
                // Tilt gently around the neck; shoulders stay planted.
                px-=motionStrength*turn*.011f*(v-.43f)*rect.height*head;
                py+=motionStrength*turn*.011f*(u-.5f)*rect.width*head;
                var vertex=UIVertex.simpleVert;vertex.position=new Vector3(rect.center.x+px,rect.center.y+py);
                vertex.uv0=new Vector2(u,v);vertex.color=color;mesh.AddVert(vertex);
            }
            for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
            {
                int i=y*(columns+1)+x;
                mesh.AddTriangle(i,i+columns+1,i+1);mesh.AddTriangle(i+1,i+columns+1,i+columns+2);
            }
        }
        protected override void OnDisable()
        {
            var instance=animatedMaterial;
            if(instance) {material=sourceMaterial;animatedMaterial=null;sourceMaterial=null;}
            base.OnDisable();
            if(instance) {if(Application.isPlaying) Destroy(instance);else DestroyImmediate(instance);}
        }
    }
}
