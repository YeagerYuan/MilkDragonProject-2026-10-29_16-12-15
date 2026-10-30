using UnityEngine;

public class DocumentInteractable : MonoBehaviour
{
    [SerializeField] private DocumentData documentData;

    public void Interact()
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
        DocumentManager.Instance.OpenDocument(documentData);
    }
}