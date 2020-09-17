
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    [AddComponentMenu("Udon Sharp/Video/Sync Mode Controller")]
    public class SyncModeController : UdonSharpBehaviour
    {
        public USharpVideoPlayer videoPlayer;

        public RectTransform sliderTransform;
        public float transformWidth;

        Animator _animator;
        Text _sliderText;
        float _streamXTarget;

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _sliderText = sliderTransform.GetComponentInChildren<Text>();
            //_streamXTarget = ((RectTransform)transform).rect.width * 0.5f;
            _streamXTarget = transformWidth * 0.5f;
        }

        public void SetVideoVisual()
        {
            _animator.SetInteger("Target", 0);
            _sliderText.text = "Video";
            videoPlayer.currentPlayerMode = 0;
        }

        public void SetStreamVisual()
        {
            _animator.SetInteger("Target", 1);
            _sliderText.text = "Stream";
            videoPlayer.currentPlayerMode = 1;
        }

        public void ClickVideoToggle()
        {
            videoPlayer.SetVideoSyncMode();
        }

        public void ClickStreamToggle()
        {
            videoPlayer.SetStreamSyncMode();
        }
    }
}
