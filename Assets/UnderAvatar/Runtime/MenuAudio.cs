using UnityEngine;

namespace UnderAvatar
{
    public sealed class MenuAudio : MonoBehaviour
    {
        PrototypeApp app;
        AudioSource music,page;
        public bool MusicPlaying => music && music.isPlaying;
        public void Initialize(PrototypeApp owner)
        {
            app=owner;
            music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.loop=true;music.spatialBlend=0;
            page=gameObject.AddComponent<AudioSource>();page.playOnAwake=false;page.spatialBlend=0;
        }
        void Update()
        {
            if(!app) return;
            var profile=app.AudioProfile;
            var clip=profile?profile.menuMusic:null;
            if(music.clip!=clip) {music.Stop();music.clip=clip;}
            music.volume=profile?profile.menuMusicVolume:0;
            bool allowed=!app.Session || app.Session.IsTraining;
            if(!allowed) {if(music.isPlaying) music.Stop();}
            else if(clip && !music.isPlaying) music.Play();
        }
        public void PlayPageTurn()
        {
            var profile=app.AudioProfile;
            if(profile && profile.comicPageTurn) page.PlayOneShot(profile.comicPageTurn,profile.comicPageVolume);
        }
    }
}
