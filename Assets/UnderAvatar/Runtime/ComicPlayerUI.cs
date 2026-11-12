using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UnderAvatar
{
    public sealed class ComicPlayerUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] CanvasGroup[] panels=Array.Empty<CanvasGroup>();
        [SerializeField] Button skipButton;
        [SerializeField] Text progressLabel;
        [SerializeField, Min(.01f)] float fadeSeconds=.4f;
        [SerializeField, Min(0)] float riseDistance=18;
        Vector2[] restingPositions;
        Action onFinished,onPageTurn;
        Font runtimeFont;
        int openedFrame, current=-1;
        float elapsed;
        public bool IsPlaying { get; private set; }
        public bool IsAnimating { get; private set; }
        public int RevealedCount => current+1;
        public CanvasGroup[] Panels => panels;

        public void Configure(CanvasGroup[] frames,Button skip,Text progress)
        { panels=frames;skipButton=skip;progressLabel=progress; }

        void Awake()
        {
            restingPositions=new Vector2[panels.Length];
            for(int i=0;i<panels.Length;i++) restingPositions[i]=((RectTransform)panels[i].transform).anchoredPosition;
            if(skipButton) skipButton.onClick.AddListener(Finish);
            runtimeFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Microsoft YaHei UI","SimHei","Arial"},24);
            foreach(var text in GetComponentsInChildren<Text>(true)) text.font=runtimeFont;
        }

        public void Show(Action finished,Action pageTurn=null)
        {
            if(IsPlaying) return;
            gameObject.SetActive(true);
            if(!EventSystem.current)
            {
                var events=new GameObject("ComicEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            EventSystem.current.SetSelectedGameObject(null);
            onFinished=finished;onPageTurn=pageTurn;current=-1;elapsed=0;IsAnimating=false;IsPlaying=true;openedFrame=Time.frameCount;
            for(int i=0;i<panels.Length;i++)
            {
                panels[i].alpha=0;panels[i].blocksRaycasts=false;panels[i].interactable=false;
                ((RectTransform)panels[i].transform).anchoredPosition=restingPositions[i];
            }
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            UpdateLabel();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button==PointerEventData.InputButton.Left && Time.frameCount>openedFrame) Advance();
        }

        public void Advance()
        {
            if(!IsPlaying) return;
            if(!IsAnimating && current+1>=panels.Length) { Finish();return; }
            onPageTurn?.Invoke();
            if(IsAnimating) { CompletePanel();return; }
            current++;elapsed=0;IsAnimating=true;
            ((RectTransform)panels[current].transform).anchoredPosition=restingPositions[current]-Vector2.up*riseDistance;
            UpdateLabel();
        }

        void Update()
        {
            if(!IsPlaying || !IsAnimating) return;
            elapsed+=Time.unscaledDeltaTime;
            float t=Mathf.Clamp01(elapsed/Mathf.Max(.01f,fadeSeconds));
            panels[current].alpha=t;
            ((RectTransform)panels[current].transform).anchoredPosition=restingPositions[current]-Vector2.up*riseDistance*(1-Mathf.SmoothStep(0,1,t));
            if(t>=1) CompletePanel();
        }

        void CompletePanel()
        {
            panels[current].alpha=1;
            ((RectTransform)panels[current].transform).anchoredPosition=restingPositions[current];
            IsAnimating=false;UpdateLabel();
        }
        void UpdateLabel()
        {
            if(progressLabel) progressLabel.text=current==panels.Length-1 && !IsAnimating
                ?"点击进入教学大厅":"点击继续  ·  "+RevealedCount+" / "+panels.Length;
        }
        public void Finish()
        {
            if(!IsPlaying) return;
            onPageTurn?.Invoke();var finished=onFinished;Cancel();finished?.Invoke();
        }
        public void Cancel()
        { onFinished=onPageTurn=null;IsPlaying=IsAnimating=false;gameObject.SetActive(false); }
        void OnDisable() { onFinished=onPageTurn=null;IsPlaying=IsAnimating=false; }
        void OnDestroy() { if(runtimeFont) Destroy(runtimeFont); }
    }
}
