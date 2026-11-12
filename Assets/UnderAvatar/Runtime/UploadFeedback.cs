using UnityEngine;

namespace UnderAvatar
{
    public sealed partial class MatchSession
    {
        public int UploadFeedbackId { get; private set; }
        public bool UploadFeedbackSuccess { get; private set; }
        public string UploadFeedbackTitle { get; private set; }
        public string UploadFeedbackDetail { get; private set; }
        public float UploadFeedbackUntil { get; private set; }
        void SetUploadFeedback(bool success,string title,string detail)
        {
            UploadFeedbackId++;UploadFeedbackSuccess=success;UploadFeedbackTitle=title;UploadFeedbackDetail=detail;
            UploadFeedbackUntil=Time.unscaledTime+6;
            if(!IsStreamerPlayer) audioBank.UploadFeedback(success);
        }
        void ApplyUploadFeedback(MatchSnapshot snapshot)
        {
            // Persistent sequence survives snapshot coalescing; each local cue plays exactly once.
            if(snapshot.uploadFeedbackId<=UploadFeedbackId) return;
            UploadFeedbackId=snapshot.uploadFeedbackId;UploadFeedbackSuccess=snapshot.uploadFeedbackSuccess;
            UploadFeedbackTitle=snapshot.uploadFeedbackTitle;UploadFeedbackDetail=snapshot.uploadFeedbackDetail;
            UploadFeedbackUntil=Time.unscaledTime+snapshot.uploadFeedbackRemaining;
            if(!IsStreamerPlayer && snapshot.uploadFeedbackRemaining>0) audioBank.UploadFeedback(UploadFeedbackSuccess);
        }
    }
}
