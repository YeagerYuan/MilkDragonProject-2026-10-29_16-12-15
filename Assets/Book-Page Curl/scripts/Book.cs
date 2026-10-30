//The implementation is based on this article:http://rbarraza.com/html5-canvas-pageflip/
//As the rbarraza.com website is not live anymore you can get an archived version from web archive 
//or check an archived version that I uploaded on my website: https://dandarawy.com/html5-canvas-pageflip/

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
public enum FlipMode
{
    RightToLeft,
    LeftToRight
}
[ExecuteInEditMode]
public class Book : MonoBehaviour {
    public Canvas canvas;
    [SerializeField]
    RectTransform BookPanel;
    public Sprite background;
    public Sprite[] bookPages;
    public bool interactable=true;
    public bool enableShadowEffect=true;
    //represent the index of the sprite shown in the right page
    [SerializeField] private Image LeftArtwork;
    [SerializeField] private Image LeftNextArtwork;
    [SerializeField] private Image RightArtwork;
    [SerializeField] private Image RightNextArtwork;
    public int currentPage = 0;
    public int TotalPageCount
    {
        get { return bookPages != null ? bookPages.Length : 0; }
    }
    public Vector3 EndBottomLeft
    {
        get { return ebl; }
    }
    public Vector3 EndBottomRight
    {
        get { return ebr; }
    }
    public float Height
    {
        get
        {
            return BookPanel.rect.height ; 
        }
    }
    public Image ClippingPlane;
    public Image NextPageClip;
    public Image Shadow;
    public Image ShadowLTR;
    public Image Left;
    public Image LeftNext;
    public Image Right;
    public Image RightNext;
    public UnityEvent OnFlip;
    float radius1, radius2;
    //Spine Bottom
    Vector3 sb;
    //Spine Top
    Vector3 st;
    //corner of the page
    Vector3 c;
    //Edge Bottom Right
    Vector3 ebr;
    //Edge Bottom Left
    Vector3 ebl;
    //follow point 
    Vector3 f;
    bool pageDragging = false;
    //current flip mode
    FlipMode mode;

    private bool initialized;
    private bool tweening;
    private bool diaryMode;
    private int ignoreInputFrame = -1;
    public RectTransform Bounds => BookPanel;

    // Page 1 is on the left, page 2 on the right. Pad odd counts with blank paper.
    public void SetDiaryPages(Sprite[] pages, Sprite blank)
    {
        if (pages == null) pages = new Sprite[0];
        diaryMode = true;
        background = blank;
        bookPages = new Sprite[pages.Length + pages.Length % 2];
        System.Array.Copy(pages, bookPages, pages.Length);
        if (pages.Length % 2 != 0) bookPages[bookPages.Length - 1] = blank;
        currentPage = 1;
        ignoreInputFrame = Time.frameCount;
        // The opener may supply pages after Start already initialized this Book.
        if (initialized)
        {
            Canvas.ForceUpdateCanvases();
            UpdateSprites();
        }
    }

    void Start() => InitializeBook();

    // The page Image/RectTransform still belongs to PageCurler.
    // Missing child references fall back to the original parent rendering.
    void SetPageSprite(Image page, Image artwork, Sprite sprite, bool alignRight)
    {
        if (!page) return;
        if (!artwork || artwork == page || artwork.transform.parent != page.transform)
        {
            page.enabled = true;
            page.sprite = sprite;
            page.overrideSprite = null;
            page.color = Color.white;
            return;
        }

        page.enabled = true;
        Mask mask = page.GetComponent<Mask>();
        if (mask && mask.enabled)
        {
            // A stencil mask must keep opaque geometry; hide only its drawing.
            // Assign an opaque rectangular Source Image on this parent.
            page.color = Color.white;
            mask.showMaskGraphic = false;
        }
        else
        {
            page.color = new Color(1, 1, 1, 0);
        }

        artwork.gameObject.SetActive(true);
        artwork.enabled = true;
        artwork.sprite = sprite;
        artwork.overrideSprite = null;
        artwork.color = Color.white;
        artwork.type = Image.Type.Simple;
        artwork.preserveAspect = false;
        artwork.raycastTarget = false;
        artwork.maskable = true;

        RectTransform pageRT = page.rectTransform;
        RectTransform artRT = artwork.rectTransform;
        Vector2 anchor = new Vector2(alignRight ? 1f : 0f, 0.5f);
        artRT.anchorMin = artRT.anchorMax = artRT.pivot = anchor;
        artRT.anchoredPosition3D = Vector3.zero;
        artRT.localScale = Vector3.one;
        artRT.localRotation = Quaternion.identity;

        float width = Mathf.Max(0, pageRT.rect.width);
        float height = Mathf.Max(0, pageRT.rect.height);
        if (sprite && sprite.rect.width > 0 && sprite.rect.height > 0)
        {
            float scale = Mathf.Min(width / sprite.rect.width, height / sprite.rect.height);
            width = sprite.rect.width * scale;
            height = sprite.rect.height * scale;
        }
        else
        {
            // A null sprite is a blank page, not an opaque white child quad.
            artwork.color = new Color(1, 1, 1, 0);
        }
        artRT.sizeDelta = new Vector2(width, height);
    }

    public void InitializeBook()
    {
        if (initialized) return;
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!canvas || !BookPanel || !Left || !Right || !LeftNext || !RightNext ||
            !ClippingPlane || !NextPageClip || !Shadow || !ShadowLTR)
        {
            Debug.LogError("Book: assign Canvas, BookPanel, all four page Images, both clip Images and both shadow Images before initialization.", this);
            return;
        }
        if (bookPages == null) bookPages = new Sprite[0];
        Canvas.ForceUpdateCanvases();
        if (BookPanel.rect.width <= 0 || BookPanel.rect.height <= 0)
        {
            Debug.LogError("Book: BookPanel must have a non-zero size. Activate/layout the panel before calling InitializeBook().", this);
            return;
        }
        if (!LeftArtwork || !LeftNextArtwork || !RightArtwork || !RightNextArtwork)
            Debug.LogWarning("Book: some artwork fields are unassigned; those pages will use their original parent Image until assigned.", this);

        Left.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);
        UpdateSprites();
        CalcCurlCriticalPoints();

        float pageWidth = BookPanel.rect.width / 2.0f;
        float pageHeight = BookPanel.rect.height;
        NextPageClip.rectTransform.sizeDelta = new Vector2(pageWidth, pageHeight + pageHeight * 2);

        ClippingPlane.rectTransform.sizeDelta = new Vector2(pageWidth * 2 + pageHeight, pageHeight + pageHeight * 2);

        //hypotenous (diagonal) page length
        float hyp = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
        float shadowPageHeight = pageWidth / 2 + hyp;

        Shadow.rectTransform.sizeDelta = new Vector2(pageWidth, shadowPageHeight);
        Shadow.rectTransform.pivot = new Vector2(1, (pageWidth / 2) / shadowPageHeight);

        ShadowLTR.rectTransform.sizeDelta = new Vector2(pageWidth, shadowPageHeight);
        ShadowLTR.rectTransform.pivot = new Vector2(0, (pageWidth / 2) / shadowPageHeight);
        // Commit only after all initialization steps have succeeded.
        initialized = true;

    }

    private void CalcCurlCriticalPoints()
    {
        sb = new Vector3(0, -BookPanel.rect.height / 2);
        ebr = new Vector3(BookPanel.rect.width / 2, -BookPanel.rect.height / 2);
        ebl = new Vector3(-BookPanel.rect.width / 2, -BookPanel.rect.height / 2);
        st = new Vector3(0, BookPanel.rect.height / 2);
        radius1 = Vector2.Distance(sb, ebr);
        float pageWidth = BookPanel.rect.width / 2.0f;
        float pageHeight = BookPanel.rect.height;
        radius2 = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
    }

    public Vector3 transformPoint(Vector3 mouseScreenPos)
    {
        Canvas root = canvas.rootCanvas;
        Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        Vector2 point;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(BookPanel, mouseScreenPos, camera, out point);
        return point;
    }
    private Vector3 PointerPosition => Mouse.current != null
        ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;

    void Update()
    {
        if (pageDragging && interactable)
        {
            UpdateBook();
            // PointerUp must work even when the cursor leaves a hotspot/the book.
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed) ReleasePage();
        }
    }
    public void UpdateBook()
    {
        f = Vector3.Lerp(f, transformPoint(PointerPosition), Time.unscaledDeltaTime * 10);
        if (mode == FlipMode.RightToLeft)
            UpdateBookRTLToPoint(f);
        else
            UpdateBookLTRToPoint(f);
    }
    public void UpdateBookLTRToPoint(Vector3 followLocation)
    {
        mode = FlipMode.LeftToRight;
        f = followLocation;
        ShadowLTR.transform.SetParent(ClippingPlane.transform, true);
        ShadowLTR.transform.localPosition = new Vector3(0, 0, 0);
        ShadowLTR.transform.localEulerAngles = new Vector3(0, 0, 0);
        Left.transform.SetParent(ClippingPlane.transform, true);

        Right.transform.SetParent(BookPanel.transform, true);
        Right.transform.localEulerAngles = Vector3.zero;
        LeftNext.transform.SetParent(BookPanel.transform, true);

        c = Calc_C_Position(followLocation);
        Vector3 t1;
        float clipAngle = CalcClipAngle(c, ebl, out t1);
        //0 < T0_T1_Angle < 180
        clipAngle = (clipAngle + 180) % 180;

        ClippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
        ClippingPlane.transform.position = BookPanel.TransformPoint(t1);

        //page position and angle
        Left.transform.position = BookPanel.TransformPoint(c);
        float C_T1_dy = t1.y - c.y;
        float C_T1_dx = t1.x - c.x;
        float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
        Left.transform.localEulerAngles = new Vector3(0, 0, C_T1_Angle - 90 - clipAngle);

        NextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
        NextPageClip.transform.position = BookPanel.TransformPoint(t1);
        LeftNext.transform.SetParent(NextPageClip.transform, true);
        Right.transform.SetParent(ClippingPlane.transform, true);
        Right.transform.SetAsFirstSibling();

        ShadowLTR.rectTransform.SetParent(Left.rectTransform, true);
    }
    public void UpdateBookRTLToPoint(Vector3 followLocation)
    {
        mode = FlipMode.RightToLeft;
        f = followLocation;
        Shadow.transform.SetParent(ClippingPlane.transform, true);
        Shadow.transform.localPosition = Vector3.zero;
        Shadow.transform.localEulerAngles = Vector3.zero;
        Right.transform.SetParent(ClippingPlane.transform, true);

        Left.transform.SetParent(BookPanel.transform, true);
        Left.transform.localEulerAngles = Vector3.zero;
        RightNext.transform.SetParent(BookPanel.transform, true);
        c = Calc_C_Position(followLocation);
        Vector3 t1;
        float clipAngle = CalcClipAngle(c, ebr, out t1);
        if (clipAngle > -90) clipAngle += 180;

        ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);
        ClippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
        ClippingPlane.transform.position = BookPanel.TransformPoint(t1);

        //page position and angle
        Right.transform.position = BookPanel.TransformPoint(c);
        float C_T1_dy = t1.y - c.y;
        float C_T1_dx = t1.x - c.x;
        float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
        Right.transform.localEulerAngles = new Vector3(0, 0, C_T1_Angle - (clipAngle + 90));

        NextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
        NextPageClip.transform.position = BookPanel.TransformPoint(t1);
        RightNext.transform.SetParent(NextPageClip.transform, true);
        Left.transform.SetParent(ClippingPlane.transform, true);
        Left.transform.SetAsFirstSibling();

        Shadow.rectTransform.SetParent(Right.rectTransform, true);
    }
    private float CalcClipAngle(Vector3 c,Vector3 bookCorner,out  Vector3 t1)
    {
        Vector3 t0 = (c + bookCorner) / 2;
        float T0_CORNER_dy = bookCorner.y - t0.y;
        float T0_CORNER_dx = bookCorner.x - t0.x;
        float T0_CORNER_Angle = Mathf.Atan2(T0_CORNER_dy, T0_CORNER_dx);
        float T0_T1_Angle = 90 - T0_CORNER_Angle;
        
        float T1_X = t0.x - T0_CORNER_dy * Mathf.Tan(T0_CORNER_Angle);
        T1_X = normalizeT1X(T1_X, bookCorner, sb);
        t1 = new Vector3(T1_X, sb.y, 0);
        
        //clipping plane angle=T0_T1_Angle
        float T0_T1_dy = t1.y - t0.y;
        float T0_T1_dx = t1.x - t0.x;
        T0_T1_Angle = Mathf.Atan2(T0_T1_dy, T0_T1_dx) * Mathf.Rad2Deg;
        return T0_T1_Angle;
    }
    private float normalizeT1X(float t1,Vector3 corner,Vector3 sb)
    {
        if (t1 > sb.x && sb.x > corner.x)
            return sb.x;
        if (t1 < sb.x && sb.x < corner.x)
            return sb.x;
        return t1;
    }
    private Vector3 Calc_C_Position(Vector3 followLocation)
    {
        Vector3 c;
        f = followLocation;
        float F_SB_dy = f.y - sb.y;
        float F_SB_dx = f.x - sb.x;
        float F_SB_Angle = Mathf.Atan2(F_SB_dy, F_SB_dx);
        Vector3 r1 = new Vector3(radius1 * Mathf.Cos(F_SB_Angle),radius1 * Mathf.Sin(F_SB_Angle), 0) + sb;

        float F_SB_distance = Vector2.Distance(f, sb);
        if (F_SB_distance < radius1)
            c = f;
        else
            c = r1;
        float F_ST_dy = c.y - st.y;
        float F_ST_dx = c.x - st.x;
        float F_ST_Angle = Mathf.Atan2(F_ST_dy, F_ST_dx);
        Vector3 r2 = new Vector3(radius2 * Mathf.Cos(F_ST_Angle),
           radius2 * Mathf.Sin(F_ST_Angle), 0) + st;
        float C_ST_distance = Vector2.Distance(c, st);
        if (C_ST_distance > radius2)
            c = r2;
        return c;
    }
    public void DragRightPageToPoint(Vector3 point)
    {
        if (!initialized || tweening || pageDragging || Time.frameCount == ignoreInputFrame) return;
        if (currentPage >= (diaryMode ? bookPages.Length - 1 : bookPages.Length)) return;
        pageDragging = true;
        mode = FlipMode.RightToLeft;
        f = point;


        NextPageClip.rectTransform.pivot = new Vector2(0, 0.12f);
        ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);

        Left.gameObject.SetActive(true);
        Left.rectTransform.pivot = new Vector2(0, 0);
        Left.transform.position = RightNext.transform.position;
        Left.transform.eulerAngles = new Vector3(0, 0, 0);
        SetPageSprite(Left, LeftArtwork, (currentPage < bookPages.Length) ? bookPages[currentPage] : background, true);
        Left.transform.SetAsFirstSibling();
        
        Right.gameObject.SetActive(true);
        Right.transform.position = RightNext.transform.position;
        Right.transform.eulerAngles = new Vector3(0, 0, 0);
        SetPageSprite(Right, RightArtwork, (currentPage < bookPages.Length - 1) ? bookPages[currentPage + 1] : background, false);

        SetPageSprite(RightNext, RightNextArtwork, (currentPage < bookPages.Length - 2) ? bookPages[currentPage + 2] : background, false);

        LeftNext.transform.SetAsFirstSibling();
        if (enableShadowEffect) Shadow.gameObject.SetActive(true);
        UpdateBookRTLToPoint(f);
    }
    public void OnMouseDragRightPage()
    {
        if (interactable)
        DragRightPageToPoint(transformPoint(PointerPosition));
        
    }
    public void DragLeftPageToPoint(Vector3 point)
    {
        if (!initialized || tweening || pageDragging || Time.frameCount == ignoreInputFrame) return;
        if (currentPage <= (diaryMode ? 1 : 0)) return;
        pageDragging = true;
        mode = FlipMode.LeftToRight;
        f = point;

        NextPageClip.rectTransform.pivot = new Vector2(1, 0.12f);
        ClippingPlane.rectTransform.pivot = new Vector2(0, 0.35f);

        Right.gameObject.SetActive(true);
        Right.transform.position = LeftNext.transform.position;
        SetPageSprite(Right, RightArtwork, (currentPage > 0 && currentPage <= bookPages.Length) ? bookPages[currentPage - 1] : background, false);
        Right.transform.eulerAngles = new Vector3(0, 0, 0);
        Right.transform.SetAsFirstSibling();

        Left.gameObject.SetActive(true);
        Left.rectTransform.pivot = new Vector2(1, 0);
        Left.transform.position = LeftNext.transform.position;
        Left.transform.eulerAngles = new Vector3(0, 0, 0);
        SetPageSprite(Left, LeftArtwork, (currentPage >= 2) ? bookPages[currentPage - 2] : background, true);

        SetPageSprite(LeftNext, LeftNextArtwork, (currentPage >= 3) ? bookPages[currentPage - 3] : background, true);

        RightNext.transform.SetAsFirstSibling();
        if (enableShadowEffect) ShadowLTR.gameObject.SetActive(true);
        UpdateBookLTRToPoint(f);
    }
    public void OnMouseDragLeftPage()
    {
        if (interactable)
        DragLeftPageToPoint(transformPoint(PointerPosition));
        
    }
    public void OnMouseRelease()
    {
        if (interactable)
            ReleasePage();
    }
    public void ReleasePage()
    {
        if (pageDragging && !tweening)
        {
            pageDragging = false;
            float distanceToLeft = Vector2.Distance(c, ebl);
            float distanceToRight = Vector2.Distance(c, ebr);
            if (distanceToRight < distanceToLeft && mode == FlipMode.RightToLeft)
                TweenBack();
            else if (distanceToRight > distanceToLeft && mode == FlipMode.LeftToRight)
                TweenBack();
            else
                TweenForward();
        }
    }
    Coroutine currentCoroutine;
    void UpdateSprites()
    {
        SetPageSprite(
            LeftNext, LeftNextArtwork,
            (currentPage > 0 && currentPage <= bookPages.Length)
                ? bookPages[currentPage - 1] : background,
            true
        );

        SetPageSprite(
            RightNext, RightNextArtwork,
            (currentPage >= 0 && currentPage < bookPages.Length)
                ? bookPages[currentPage] : background,
            false
        );
    }
    public void TweenForward()
    {
        if(mode== FlipMode.RightToLeft)
        currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f, () => { Flip(); }));
        else
        currentCoroutine = StartCoroutine(TweenTo(ebr, 0.15f, () => { Flip(); }));
    }
    void Flip()
    {
        if (mode == FlipMode.RightToLeft)
            currentPage += 2;
        else
            currentPage -= 2;
        LeftNext.transform.SetParent(BookPanel.transform, true);
        Left.transform.SetParent(BookPanel.transform, true);
        LeftNext.transform.SetParent(BookPanel.transform, true);
        Left.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);
        Right.transform.SetParent(BookPanel.transform, true);
        RightNext.transform.SetParent(BookPanel.transform, true);
        UpdateSprites();
        Shadow.gameObject.SetActive(false);
        ShadowLTR.gameObject.SetActive(false);
        if (OnFlip != null)
            OnFlip.Invoke();
    }
    public void TweenBack()
    {
        if (mode == FlipMode.RightToLeft)
        {
            currentCoroutine = StartCoroutine(TweenTo(ebr,0.15f,
                () =>
                {
                    UpdateSprites();
                    RightNext.transform.SetParent(BookPanel.transform);
                    Right.transform.SetParent(BookPanel.transform);

                    Left.gameObject.SetActive(false);
                    Right.gameObject.SetActive(false);
                    pageDragging = false;
                }
                ));
        }
        else
        {
            currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f,
                () =>
                {
                    UpdateSprites();

                    LeftNext.transform.SetParent(BookPanel.transform);
                    Left.transform.SetParent(BookPanel.transform);

                    Left.gameObject.SetActive(false);
                    Right.gameObject.SetActive(false);
                    pageDragging = false;
                }
                ));
        }
    }
    public IEnumerator TweenTo(Vector3 to, float duration, System.Action onFinish)
    {
        tweening = true;
        int steps = Mathf.Max(1, (int)(duration / 0.025f));
        Vector3 displacement = (to - f) / steps;
        for (int i = 0; i < steps-1; i++)
        {
            if(mode== FlipMode.RightToLeft)
            UpdateBookRTLToPoint( f + displacement);
            else
                UpdateBookLTRToPoint(f + displacement);

            yield return new WaitForSecondsRealtime(0.025f);
        }
        if (onFinish != null)
            onFinish();
        tweening = false;
    }
}
