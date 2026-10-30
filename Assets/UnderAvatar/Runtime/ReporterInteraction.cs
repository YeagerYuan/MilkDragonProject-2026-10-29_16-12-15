using UnityEngine;
using UnityEngine.InputSystem;
using UnderAvatar;

public class ReporterInteraction : MonoBehaviour
{
    private ReporterController reporter;

    [SerializeField] private float interactionDistance = 5f;

    void Awake()
    {
        reporter = GetComponent<ReporterController>();
    }

void Update()
{
    if (DocumentManager.BlocksGameplayInput) return;
    if (Mouse.current != null &&
        Mouse.current.leftButton.wasPressedThisFrame)
    {
        Debug.Log("Mouse clicked!");
        Interact();
    }
}

    void Interact()
    {
        if (reporter == null || reporter.View == null)
        {
            Debug.LogError("Reporter camera not found!");
            return;
        }

        Ray ray = reporter.View.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            Debug.Log("Raycast hit: " + hit.collider.name);

            DocumentInteractable document =
                hit.collider.GetComponentInParent<DocumentInteractable>();

            if (document != null)
            {
                document.Interact();
            }
            else
            {
                Debug.Log("Object has no DocumentInteractable!");
            }
        }
        else
        {
            Debug.Log("Raycast hit nothing!");
        }
    }
}