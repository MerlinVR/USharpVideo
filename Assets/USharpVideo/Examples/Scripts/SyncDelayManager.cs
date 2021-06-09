
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace UdonSharp.Video.Examples
{
    /// <summary>
    /// Example sync delay script used for locally delaying sync by some amount of time.
    /// This can be used for something like karaoke where one person is singing a video and remote players want to hear the voice synced with the video audio.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Examples/Sync Delay Manager")]
    public class SyncDelayManager : UdonSharpBehaviour
    {
        public USharpVideoPlayer videoPlayer;

        public float maxDelay = 2f;

        public Slider delaySlider;
        public Text delayReadout;

        private void Start()
        {
            if (videoPlayer == null)
                Debug.LogError("SyncDelayManager must have a video player reference!", this);
        }

        public void OnSliderChange()
        {
            float sliderVal = delaySlider.value;

            float timeValue = sliderVal * maxDelay;

            delayReadout.text = $"{(int)(timeValue * 1000f)} ms";

            videoPlayer.localSyncOffset = -timeValue;
            videoPlayer.ForceSyncVideo();
        }

        public void OnResyncPress()
        {
            videoPlayer.ForceSyncVideo();
        }
    }
}
