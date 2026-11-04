using UnityEngine;

public class DocumentInteractable : MonoBehaviour
{
    [SerializeField] private DocumentData documentData;
    [Tooltip("可选：此文档作为拍照任务目标时的唯一标识。同一关卡内不能重复；留空则只供阅读。")]
    [SerializeField] private string photoTargetId="";
    [Tooltip("可选：拍照判定点。不填则使用此物体的碰撞体中心。")]
    [SerializeField] private Transform photoPoint;
    public string PhotoTargetId => string.IsNullOrWhiteSpace(photoTargetId)?null:UnderAvatar.InvestigationData.DocumentTargetPrefix+photoTargetId.Trim();
    public Vector3 PhotoPoint => photoPoint?photoPoint.position:GetComponent<Collider>() is Collider body?body.bounds.center:transform.position;
    public DocumentData Data => documentData;
    public void Initialize(DocumentData data) { documentData=data; }
    public string DisplayName => documentData && !string.IsNullOrWhiteSpace(documentData.documentName)?documentData.documentName:gameObject.name;

    public void Interact(UnderAvatar.MatchSession owner=null)
    {
        if(!isActiveAndEnabled) return;
        if (documentData == null)
        {
            Debug.LogWarning("No document data assigned!");
            return;
        }

        if (DocumentManager.BlocksGameplayInput) return;
        if (DocumentManager.Instance == null)
        {
            Debug.LogWarning("No active DocumentManager in the scene!", this);
            return;
        }
        DocumentManager.Instance.OpenDocument(documentData,owner);
    }
}
