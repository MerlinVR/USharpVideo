
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace UdonSharp.Video
{
    [DefaultExecutionOrder(10)]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/UI/Video Control Handler")]
    public class VideoControlHandler : UdonSharpBehaviour
    {
        /// <summary>
        /// The video player this UI instance controls and pulls info from
        /// </summary>
        [PublicAPI, NotNull]
        public USharpVideoPlayer targetVideoPlayer;
        
#pragma warning disable CS0649
        [SerializeField]
        private VRCUrlInputField urlField;

        [SerializeField]
        private Text urlFieldPlaceholderText;

        [Header("Status text")]
        [SerializeField]
        private Text statusTextField;

        [SerializeField]
        private Text statusTextDropShadow;

        [Header("Video progress bar")]
        [SerializeField]
        private Slider progressSlider;

        [Header("Lock button")]
        [SerializeField]
        private Graphic lockGraphic;

        [SerializeField]
        private GameObject masterLockedIcon, masterUnlockedIcon;

        [Header("Info panel fields")]
        [SerializeField]
        private Text masterField;

        [SerializeField]
        private Text ownerField;

        [SerializeField]
        private InputField currentURLField, previousURLField;

        [Header("Play/Pause/Stop buttons")]
        [SerializeField]
        private GameObject pauseStopObject;

        [SerializeField]
        private GameObject playObject;

        [SerializeField]
        private GameObject pauseIcon, stopIcon;

        [Header("Loop button")]
        [SerializeField]
        private Graphic loopButtonBackground;

        [SerializeField]
        private Graphic loopButtonIcon;

        [Header("Video/Stream controls")]
        [SerializeField]
        private SyncModeController syncController;

        [Header("Volume")]
        [SerializeField]
        private VolumeController volumeController;

        [Header("Style Colors")]
        public Color redGraphicColor = new Color(0.632f, 0.19f, 0.19f);
        public Color whiteGraphicColor = new Color(0.9433f, 0.9433f, 0.9433f);
        public Color buttonBackgroundColor = new Color(1f, 1f, 1f, 1f);
        public Color buttonActivatedColor = new Color(1f, 1f, 1f, 1f);
        public Color iconInvertedColor = new Color(1f, 1f, 1f, 1f);
#pragma warning restore CS0649

        private void OnEnable()
        {
            targetVideoPlayer.RegisterControlHandler(this);
            UpdateMaster();
            UpdateVideoOwner();

            if (volumeController) volumeController.SetControlHandler(this);
        }

        private void OnDisable()
        {
            targetVideoPlayer.UnregisterControlHandler(this);
        }

        // Only allow the master to own this so we can check master by checking this object's owner
        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            return false;
        }

        private void Update()
        {
            RunUIUpdate();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            UpdateMaster();
        }

        public void OnVideoPlayerOwnerTransferred()
        {
            UpdateVideoOwner();
        }

        void UpdateMaster()
        {
#if !UNITY_EDITOR
            // We know the owner of this will always be the master so just get the owner and update the name
            if (masterField)
            {
                VRCPlayerApi owner = Networking.GetOwner(gameObject);
                if (owner != null && owner.IsValid())
                    masterField.text = Networking.GetOwner(gameObject).displayName;
            }
#endif
        }

        private void UpdateVideoOwner()
        {
#if !UNITY_EDITOR
            if (ownerField)
                ownerField.text = Networking.GetOwner(targetVideoPlayer.gameObject).displayName;
#endif

            SetLocked(targetVideoPlayer.IsLocked());
        }

        [PublicAPI]
        public void SetControlledVideoPlayer(USharpVideoPlayer newPlayer)
        {
            if (newPlayer == targetVideoPlayer)
                return;

            targetVideoPlayer.UnregisterControlHandler(this);
            targetVideoPlayer = newPlayer;
            targetVideoPlayer.RegisterControlHandler(this);
            UpdateVideoOwner();

            SetStatusText("");
            _draggingSlider = false;
        }

        string _currentStatusText = "";

        public void SetStatusText(string newStatus)
        {
            _currentStatusText = newStatus;
            if (statusTextField) statusTextField.text = _currentStatusText;
            if (statusTextDropShadow) statusTextDropShadow.text = _currentStatusText;
            _lastTime = int.MaxValue;
        }
        
        public void SetLocked(bool locked)
        {
            if (locked)
            {
                if (masterLockedIcon) masterLockedIcon.SetActive(true);
                if (masterUnlockedIcon) masterUnlockedIcon.SetActive(false);

                if (Networking.IsOwner(targetVideoPlayer.gameObject) || targetVideoPlayer.CanControlVideoPlayer())
                {
                    if (lockGraphic) lockGraphic.color = whiteGraphicColor;
                    if (urlFieldPlaceholderText) urlFieldPlaceholderText.text = "Enter Video URL...";
                }
                else
                {
                    if (lockGraphic) lockGraphic.color = redGraphicColor;
                    if (urlFieldPlaceholderText) urlFieldPlaceholderText.text = $"Only the master {Networking.GetOwner(targetVideoPlayer.gameObject).displayName} may add URLs";
                }
            }
            else
            {
                if (masterLockedIcon) masterLockedIcon.SetActive(false);
                if (masterUnlockedIcon) masterUnlockedIcon.SetActive(true);
                if (lockGraphic) lockGraphic.color = whiteGraphicColor;
                if (urlFieldPlaceholderText) urlFieldPlaceholderText.text = "Enter Video URL... (anyone)";
            }
        }
        
        public void SetPaused(bool paused)
        {
            bool videoMode = targetVideoPlayer.IsInVideoMode();

            if (pauseIcon) pauseIcon.SetActive(videoMode);
            if (stopIcon) stopIcon.SetActive(!videoMode);

            if (playObject) playObject.SetActive(paused);
            if (pauseStopObject) pauseStopObject.SetActive(!paused);
        }

        public void SetLooping(bool looping)
        {
            if (looping)
            {
                if (loopButtonBackground) loopButtonBackground.color = buttonActivatedColor;
                if (loopButtonIcon) loopButtonIcon.color = iconInvertedColor;
            }
            else
            {
                if (loopButtonBackground) loopButtonBackground.color = buttonBackgroundColor;
                if (loopButtonIcon) loopButtonIcon.color = whiteGraphicColor;
            }
        }

        public void SetVolume(float volume)
        {
            if (volumeController) volumeController.SetVolume(volume);
        }

        public void SetMuted(bool muted)
        {
            if (volumeController) volumeController.SetMuted(muted);
        }

        public void OnVolumeSliderChange(float volume)
        {
            targetVideoPlayer.SetVolume(volume);
        }

        public void OnMutePress(bool muted)
        {
            targetVideoPlayer.SetMuted(muted);
        }

        /// <summary>
        /// Adds a URL to the history display so people can copy it
        /// </summary>
        /// <param name="url"></param>
        public void AddURLToHistory(VRCUrl url)
        {
            if (currentURLField)
            {
                if (previousURLField)
                    previousURLField.text = currentURLField.text;

                currentURLField.text = url.Get();
            }
        }

        public void SetToVideoPlayerMode()
        {
            if (syncController)
                syncController.SetVideoVisual();
        }

        public void SetToStreamPlayerMode()
        {
            if (syncController)
                syncController.SetStreamVisual();
        }

        private int _lastTime = int.MaxValue;

        /// <summary>
        /// Updates UI elements such as the time readout, URL views, and seek bar
        /// </summary>
        private void RunUIUpdate()
        {
            if (targetVideoPlayer.IsInVideoMode())
            {
                VideoPlayerManager manager = targetVideoPlayer.GetVideoManager();
                float duration = manager.GetDuration();

                if (_draggingSlider)
                {
                    float currentProgress = progressSlider.value;
                    float currentTime = duration * currentProgress;

                    targetVideoPlayer.SeekTo(currentProgress);

                    string currentTimeStr = GetFormattedTime(System.TimeSpan.FromSeconds(currentTime));

                    if (statusTextField) statusTextField.text = currentTimeStr;
                    if (statusTextDropShadow) statusTextDropShadow.text = currentTimeStr;
                }
                else
                {
                    float currentTime = manager.GetTime();

                    if (progressSlider)
                    {
                        if (duration > 0f)
                        {
                            progressSlider.gameObject.SetActive(true);
                            progressSlider.value = Mathf.Clamp01(currentTime / duration);
                        }
                        else
                        {
                            progressSlider.gameObject.SetActive(false);
                        }
                    }

                    int currentTimeInt = Mathf.RoundToInt(currentTime);
                    if (currentTimeInt != _lastTime)
                    {
                        _lastTime = currentTimeInt;

                        if (!float.IsInfinity(duration) & duration != float.MaxValue)
                        {
                            if (string.IsNullOrEmpty(_currentStatusText))
                            {
                                System.TimeSpan durationTimespan = System.TimeSpan.FromSeconds(duration);
                                System.TimeSpan currentTimeTimespan = System.TimeSpan.FromSeconds(currentTime);

                                string totalTimeStr = GetFormattedTime(durationTimespan);
                                string currentTimeStr = GetFormattedTime(currentTimeTimespan);

                                string statusStr = currentTimeStr + "/" + totalTimeStr;

                                if (statusTextField) statusTextField.text = statusStr;
                                if (statusTextDropShadow) statusTextDropShadow.text = statusStr;
                            }
                        }
                    }
                }
            }
            else
            {
                if (progressSlider) progressSlider.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Gets a time string in the format hh:mm:ss, handles hours properly for long-running videos that wrap the hh section.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private string GetFormattedTime(System.TimeSpan time)
        {
            return ((int)time.TotalHours).ToString("D2") + time.ToString(@"\:mm\:ss");
        }

        /// <summary>
        /// Called when the user enters a URL in the url input field, forwards the input to the video player
        /// </summary>
        public void OnURLInput()
        {
            targetVideoPlayer.PlayVideo(urlField.GetUrl());
            urlField.SetUrl(VRCUrl.Empty);
        }

        /// <summary>
        /// Fired when the play button, pause button, or stop button are pressed. 
        /// </summary>
        public void OnPlayButtonPress()
        {
            targetVideoPlayer.SetPaused(!targetVideoPlayer.IsPaused());
        }

        public void OnLockButtonPress()
        {
            if (targetVideoPlayer.IsPrivilegedUser(Networking.LocalPlayer))
            {
                targetVideoPlayer.TakeOwnership();
                targetVideoPlayer.SetLocked(!targetVideoPlayer.IsLocked());
            }
        }

        public void OnReloadButtonPressed()
        {
            targetVideoPlayer.Reload();
        }

        public void OnLoopButtonPressed()
        {
            targetVideoPlayer.TakeOwnership();
            targetVideoPlayer.SetLooping(!targetVideoPlayer.IsLooping());
        }

        public void OnVideoPlayerModeButtonPressed()
        {
            targetVideoPlayer.SetToUnityPlayer();
        }

        public void OnStreamPlayerModeButtonPressed()
        {
            targetVideoPlayer.SetToAVProPlayer();
        }

        public void OnSeekSliderChanged()
        {
            //if (!_draggingSlider)
            //    return;


        }

        private bool _draggingSlider;

        public void OnSeekSliderBeginDrag()
        {
            if (Networking.IsOwner(targetVideoPlayer.gameObject))
                _draggingSlider = true;
        }

        public void OnSeekSliderEndDrag()
        {
            _draggingSlider = false;
        }
    }
}
