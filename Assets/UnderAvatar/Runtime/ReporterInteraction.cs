using UnityEngine;
using UnityEngine.InputSystem;
using UnderAvatar;

// Runs after session input capture, before either controller or LAN input sampling.
[DefaultExecutionOrder(-90)]
public sealed class ReporterInteraction : MonoBehaviour
{
    ReporterController reporter;
    [SerializeField,Min(.1f)] float interactionDistance=5f;
    public DocumentInteractable Target { get; private set; }
    public bool CanRead => reporter && reporter.IsHuman && !reporter.Hidden && !reporter.PhoneOpen && !(reporter.Session?.TaskWorld?.Held) &&
        reporter.Session && !reporter.Session.Finished && !reporter.Session.DebugOpen &&
        reporter.Session.InputCaptured && !DocumentManager.BlocksGameplayInput;
    void Awake() { reporter=GetComponent<ReporterController>(); }
    public DocumentInteractable FindTarget()
    {
        if(!reporter || !reporter.View) return null;
        var ray=reporter.View.ViewportPointToRay(new Vector3(.5f,.5f));
        int mask=~LayerMask.GetMask("UA_Reporter","UA_Sound");
        var document=Physics.Raycast(ray,out var hit,interactionDistance,mask,QueryTriggerInteraction.Ignore)
            ?hit.collider.GetComponentInParent<DocumentInteractable>():null;
        return document && document.isActiveAndEnabled?document:null;
    }
    public bool TryRead()
    {
        if(!CanRead) return false;
        Target=FindTarget();if(!Target) return false;
        Target.Interact(reporter.Session);
        return DocumentManager.Instance && DocumentManager.Instance.IsOpen;
    }
    void Update()
    {
        Target=CanRead?FindTarget():null;
        if(Target && Mouse.current?.leftButton.wasPressedThisFrame==true) TryRead();
    }
}
