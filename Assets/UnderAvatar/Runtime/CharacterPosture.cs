using UnityEngine;

namespace UnderAvatar
{
    public sealed class CharacterPosture
    {
        readonly CharacterController body;
        readonly Transform head,visual;
        readonly float eyeHeight;
        public bool Crouched { get; private set; }
        public CharacterPosture(CharacterController controller,Transform camera,Transform model,float standingEyeHeight)
        { body=controller; head=camera; visual=model; eyeHeight=standingEyeHeight; }
        public bool SetCrouched(bool crouch,bool authoritativeSnapshot=false)
        {
            if(Crouched==crouch) return true;
            if(!crouch && !authoritativeSnapshot)
            {
                Vector3 feet=body.transform.position;
                float radius=body.radius-.015f;
                if(Physics.CheckCapsule(feet+Vector3.up*(body.radius+.025f),feet+Vector3.up*(1.8f-body.radius),radius,
                    PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore)) return false;
            }
            Crouched=crouch;
            body.height=crouch?1f:1.8f; body.center=Vector3.up*(body.height*.5f);
            head.localPosition=Vector3.up*(crouch?.88f:eyeHeight);
            if(visual) visual.localScale=new Vector3(1,crouch?1f/1.8f:1,1);
            return true;
        }
    }
}
