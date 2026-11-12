using UnityEngine;

namespace UnderAvatar
{
    public sealed partial class PrototypeHud
    {
        Texture2D laughSkillIcon,invisibilitySkillIcon;
        void DrawCompactAbility(MatchSession match)
        {
            if(match.Finished || !match.Abilities || match.ReporterDying && !match.IsStreamerPlayer) return;
            if(!laughSkillIcon) laughSkillIcon=Resources.Load<Texture2D>("SkillIcons/LaughRed");
            if(!invisibilitySkillIcon) invisibilitySkillIcon=Resources.Load<Texture2D>("SkillIcons/InvisibilityRed");
            var ability=match.Abilities;
            bool streamer=match.IsStreamerPlayer;
            bool live=streamer && match.Streamer.State==StreamerState.Streaming;
            float value=streamer?ability.LaughEnergy/Mathf.Max(.1f,ability.Rules.laughCapacity):
                ability.Invisible?ability.InvisibleRemaining/Mathf.Max(.1f,ability.Rules.invisibilitySeconds):match.IsTraining || ability.InvisibleCharges>0?1:0;
            var previous=GUI.matrix;var color=GUI.color;
            float scale=Screen.height/800f,width=Screen.width/scale;
            try
            {
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                const float groupWidth=200,iconSize=52;
                float x=(width-groupWidth)*.5f,y=live?680:710;
                if(!live && Time.time<match.MessageUntil) y=670;
                var icon=streamer?laughSkillIcon:invisibilitySkillIcon;
                if(value<=0) GUI.color=new Color(.6f,.3f,.3f,.8f);
                if(icon) GUI.DrawTexture(new Rect(x,y,iconSize,iconSize),icon,ScaleMode.ScaleToFit,true);
                GUI.color=color;
                var key=new Rect(x+66,y+12,28,28);
                Fill(key,new Color(0,0,0,.75f));Outline(key,Color.white,1);
                GUI.Label(key,streamer?"Q":"G",new GUIStyle(notice){alignment=TextAnchor.MiddleCenter,fontSize=20});
                GUI.Label(new Rect(key.xMax+10,key.y,88,28),streamer?"大笑":"隐身",new GUIStyle(notice){fontSize=20});
                var bar=new Rect(x,y+iconSize+8,groupWidth,5);
                Fill(bar,new Color(.2f,.035f,.035f,.9f));bar.width*=Mathf.Clamp01(value);Fill(bar,new Color(.96f,.035f,.02f));
                if(!streamer)
                {
                    GUI.Label(new Rect(x+34,y-17,28,24),match.IsTraining?"∞":ability.InvisibleCharges.ToString(),new GUIStyle(notice){fontSize=18,alignment=TextAnchor.MiddleCenter});
                    var secondary=new Rect(x+66,y-28,24,24);
                    Fill(secondary,new Color(0,0,0,.7f));Outline(secondary,new Color(1,1,1,.7f),1);
                    GUI.Label(secondary,"B",new GUIStyle(notice){alignment=TextAnchor.MiddleCenter,fontSize=16});
                    GUI.Label(new Rect(secondary.xMax+8,secondary.y,100,24),"假人 ×"+(match.IsTraining?"∞":ability.DecoyCharges.ToString()),new GUIStyle(small){fontSize=16});
                }
            }
            finally {GUI.matrix=previous;GUI.color=color;}
        }
    }
}
