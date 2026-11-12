using UnityEngine;

namespace UnderAvatar
{
    // Runs on each listener's machine; never changes AI hearing or network noise events.
    [DefaultExecutionOrder(500)]
    public sealed class FloorSpatialAudio : MonoBehaviour
    {
        MatchSession session;
        AudioSource source;
        AudioLowPassFilter lowPass;
        GameAudioProfile profile;
        public float BaseVolume {get;set;}
        public int FloorDifference {get;private set;}
        public void Initialize(MatchSession match,AudioSource audio,GameAudioProfile settings,float volume)
        {
            session=match;source=audio;profile=settings;BaseVolume=volume;
            lowPass=gameObject.AddComponent<AudioLowPassFilter>();lowPass.lowpassResonanceQ=1;
            Refresh();
        }
        public static int FloorAt(float height,BuildingData data) => Mathf.Clamp(Mathf.FloorToInt((height+.15f)/data.floorHeight),0,data.floors.Count-1);
        public void Refresh()
        {
            if(!session || !source || !session.Reporter || !session.Streamer) return;
            var listener=session.IsStreamerPlayer?session.Streamer.transform:session.Reporter.transform;
            FloorDifference=Mathf.Abs(FloorAt(transform.position.y,session.World.Data)-FloorAt(listener.position.y,session.World.Data));
            bool enabled=profile?profile.floorAttenuation:true;
            int difference=enabled?FloorDifference:0;
            source.volume=BaseVolume*Mathf.Pow(profile?Mathf.Clamp01(profile.volumePerFloor):.45f,difference);
            lowPass.enabled=difference>0;
            lowPass.cutoffFrequency=difference==0?22000:Mathf.Clamp((profile?profile.otherFloorLowPass:2500)*Mathf.Pow(.6f,difference-1),500,22000);
        }
        void LateUpdate() => Refresh();
    }
}
