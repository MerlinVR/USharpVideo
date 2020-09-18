
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    [AddComponentMenu("Udon Sharp/Video/Volume Controller")]
    public class VolumeController : UdonSharpBehaviour
    {
        public AudioSource controlledAudioSource;
        public AudioSource avProAudioR;
        public AudioSource avProAudioL;
        public Slider slider;

        public GameObject muteIcon;
        public GameObject zeroVolumeIcon;
        public GameObject lowVolumeIcon;
        public GameObject HighVolumeIcon;

        bool _muted = false;

        private void Start()
        {
            ApplyVolumeSlider();
            UpdateVolumeIcon();
        }

        void ApplyVolumeSlider()
        {
            // https://www.dr-lex.be/info-stuff/volumecontrols.html#ideal thanks TCL for help with finding and understanding this
            // Using the 50dB dynamic range constants
            float volume = Mathf.Clamp01(3.1623e-3f * Mathf.Exp(slider.value * 5.757f) - 3.1623e-3f);

            controlledAudioSource.volume = volume;
            avProAudioR.volume = volume;
            avProAudioL.volume = volume;
        }

        public void SliderValueChanged()
        {
            if (_muted)
                return;

            ApplyVolumeSlider();
            UpdateVolumeIcon();
        }

        public void PressMuteButton()
        {
            _muted = !_muted;

            if (_muted)
            {
                controlledAudioSource.volume = 0f;
                avProAudioR.volume = 0f;
                avProAudioL.volume = 0f;
            }
            else
            {
                ApplyVolumeSlider();
            }

            UpdateVolumeIcon();
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
            else if (slider.value > 0.6f)
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
