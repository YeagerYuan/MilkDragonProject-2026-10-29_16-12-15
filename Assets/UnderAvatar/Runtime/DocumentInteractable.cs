using UnityEngine;

public class DocumentInteractable : MonoBehaviour
{
    [SerializeField] private DocumentData documentData;
    public DocumentData Data => documentData;
    public void Initialize(DocumentData data) { documentData=data; }
    public string DisplayName => documentData && !string.IsNullOrWhiteSpace(documentData.documentName)?documentData.documentName:gameObject.name;

    public void Interact(UnderAvatar.MatchSession owner=null)
    {
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
