
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    [AddComponentMenu("Udon Sharp/Video/UI/Volume Controller")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class VolumeController : UdonSharpBehaviour
    {
        VideoControlHandler controlHandler;

        public Slider slider;

        public GameObject muteIcon;
        public GameObject zeroVolumeIcon;
        public GameObject lowVolumeIcon;
        public GameObject HighVolumeIcon;

        bool _muted = false;

        private void Start()
        {
            UpdateVolumeIcon();
        }

        public void SetControlHandler(VideoControlHandler handler)
        {
            controlHandler = handler;
        }

        public void SetMuted(bool muted)
        {
            if (muted != _muted)
            {
                _muted = muted;
                UpdateVolumeIcon();
            }
        }

        public void SetVolume(float volume)
        {
            if (!_sliderValueChanging)
            {
                slider.value = volume;
                UpdateVolumeIcon();
            }
        }

        bool _sliderValueChanging = false;

        public void OnSliderValueChanged()
        {
            _sliderValueChanging = true;
            if (controlHandler) controlHandler.OnVolumeSliderChange(slider.value);
            _sliderValueChanging = false;

            UpdateVolumeIcon();
        }

        public void OnMutePressed()
        {
            if (controlHandler) controlHandler.OnMutePress(!_muted);
        }

        void UpdateVolumeIcon()
        {
            if (_muted)
            {
                muteIcon.SetActive(true);
                zeroVolumeIcon.SetActive(false);
                lowVolumeIcon.SetActive(false);
                HighVolumeIcon.SetActive(false);
            }
            else if (slider.value > 0.5f)
            {
                muteIcon.SetActive(false);
                zeroVolumeIcon.SetActive(false);
                lowVolumeIcon.SetActive(false);
                HighVolumeIcon.SetActive(true);
            }
            else if (slider.value > 0f)
            {
                muteIcon.SetActive(false);
                zeroVolumeIcon.SetActive(false);
                lowVolumeIcon.SetActive(true);
                HighVolumeIcon.SetActive(false);
            }
            else
            {
                muteIcon.SetActive(false);
                zeroVolumeIcon.SetActive(true);
                lowVolumeIcon.SetActive(false);
                HighVolumeIcon.SetActive(false);
            }
        }
    }
}
