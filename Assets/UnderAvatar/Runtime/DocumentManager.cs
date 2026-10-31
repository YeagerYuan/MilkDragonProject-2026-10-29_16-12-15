using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DocumentManager : MonoBehaviour
{
    public static DocumentManager Instance { get; private set; }
    private static int blockedFrame = -1;
    public static bool BlocksGameplayInput =>
        (Instance != null && Instance.IsOpen) || Time.frameCount == blockedFrame;

    [SerializeField] private GameObject documentPanel;
    [SerializeField] private Image documentImage;
    [SerializeField] private RectTransform contentRect;
    [Tooltip("Inactive scene instance of the asset's Book prefab. Setup menu assigns it.")]
    [SerializeField] private Book diaryBookTemplate;
    [Tooltip("Optional blank paper Sprite for an odd diary's final right page.")]
    [SerializeField] private Sprite blankDiaryPage;

    public bool IsOpen { get; private set; }
    private Book activeBook;
    private RectTransform activeBounds;
    private int openedFrame;
    private CursorLockMode previousLock;
    private bool previousVisibility;
    private Sprite generatedBlank;
    private Texture2D generatedTexture;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        blockedFrame = -1;
        if (contentRect == null && documentImage != null) contentRect = documentImage.rectTransform;
        if (documentPanel == null || documentImage == null || contentRect == null)
        {
            Debug.LogError("Assign Document Panel and Document Image.", this);
            enabled = false;
            return;
        }
        // Remove visibility of the previous implementation's button row, if present.
        Transform oldNavigation = documentPanel.transform.Find("DiaryNavigation");
        if (oldNavigation != null) oldNavigation.gameObject.SetActive(false);
        if (diaryBookTemplate != null) diaryBookTemplate.gameObject.SetActive(false);
        documentPanel.SetActive(false);
    }

    public void OpenDocument(DocumentData data)
    {
        if (!isActiveAndEnabled || documentPanel == null || documentImage == null) return;
        if (data == null || data.pages == null || data.pages.Length == 0)
        { Debug.LogWarning("Document has no pages!", this); return; }
        bool diary = data.documentType == DocumentType.Diary;
        int count = diary ? data.pages.Length : 1;
        for (int i = 0; i < count; i++)
            if (data.pages[i] == null)
            { Debug.LogWarning("Assign every document page Sprite.", data); return; }
        if (diary && diaryBookTemplate == null)
        {
            Debug.LogError("Run Tools > UnderAvatar > Set Up Page Curl Diary, then save the scene.", this);
            return;
        }

        if (!IsOpen)
        {
            previousLock = Cursor.lockState;
            previousVisibility = Cursor.visible;
        }
        DestroyActiveBook();
        openedFrame = Time.frameCount;
        blockedFrame = Time.frameCount;
        IsOpen = true;
        documentPanel.SetActive(true);
        documentImage.gameObject.SetActive(!diary);

        if (diary)
        {
            // A fresh instance discards any unfinished drag/tween from the last document.
            activeBook = Instantiate(diaryBookTemplate, diaryBookTemplate.transform.parent, false);
            activeBook.name = "ActiveDiaryBook";
            activeBook.canvas = documentPanel.GetComponentInParent<Canvas>();
            activeBook.SetDiaryPages(data.pages, BlankPaper());
            activeBounds = activeBook.Bounds;
            activeBook.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            FitBook();
            activeBook.InitializeBook();
        }
        else
        {
            activeBounds = contentRect;
            documentImage.sprite = data.pages[0];
            documentImage.preserveAspect = true;
            Canvas.ForceUpdateCanvases();
            FitSinglePage();
            activeBounds = documentImage.rectTransform;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private Sprite BlankPaper()
    {
        if (blankDiaryPage != null) return blankDiaryPage;
        if (generatedBlank != null) return generatedBlank;
        generatedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Color paper = new Color(.94f, .90f, .79f, 1);
        generatedTexture.SetPixels(new[] { paper, paper, paper, paper });
        generatedTexture.Apply();
        generatedBlank = Sprite.Create(generatedTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
        return generatedBlank;
    }

    private void FitBook()
    {
        var parent = activeBook.transform.parent as RectTransform;
        if (parent == null || activeBounds.rect.width <= 0 || activeBounds.rect.height <= 0) return;
        float scale = Mathf.Min(parent.rect.width * .9f / activeBounds.rect.width,
            parent.rect.height * .85f / activeBounds.rect.height);
        activeBook.transform.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }

    private void FitSinglePage()
    {
        if (documentImage.sprite == null) return;

        // Resize the actual image, not contentRect
        RectTransform rt = documentImage.rectTransform;
        RectTransform parent = rt.parent as RectTransform;

        if (parent == null) return;

        // Reset the image's transform
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        float maxWidth = parent.rect.width * 0.9f;
        float maxHeight = parent.rect.height * 0.85f;

        Sprite sprite = documentImage.sprite;
        float aspect = sprite.rect.width / sprite.rect.height;

        float width = Mathf.Min(maxWidth, maxHeight * aspect);
        float height = width / aspect;

        rt.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, width
        );

        rt.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, height
        );
    }

    public void CloseDocument()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;
        ReleaseDocument();
    }

    private void DestroyActiveBook()
    {
        if (activeBook == null) return;
        activeBook.gameObject.SetActive(false);
        Destroy(activeBook.gameObject);
        activeBook = null;
    }

    private void ReleaseDocument()
    {
        if (!IsOpen) return;
        IsOpen = false;
        blockedFrame = Time.frameCount;
        DestroyActiveBook();
        activeBounds = null;
        if (documentPanel != null) documentPanel.SetActive(false);
        if (documentImage != null) documentImage.sprite = null;
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisibility;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (!documentPanel.activeInHierarchy) { ReleaseDocument(); return; }
        if (Time.frameCount == openedFrame) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        { CloseDocument(); return; }
        if (activeBook != null) FitBook();
        // Only the initial press outside closes. Dragging/releasing outside must finish the curl.
        if (Mouse.current?.leftButton.wasPressedThisFrame != true || activeBounds == null) return;
        Canvas canvas = activeBounds.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.RectangleContainsScreenPoint(activeBounds,
                Mouse.current.position.ReadValue(), camera)) CloseDocument();
    }

    private void OnDisable() => ReleaseDocument();
    private void OnDestroy()
    {
        if (Instance != this) return;
        ReleaseDocument();
        if (generatedBlank != null) Destroy(generatedBlank);
        if (generatedTexture != null) Destroy(generatedTexture);
        Instance = null;
    }
}
