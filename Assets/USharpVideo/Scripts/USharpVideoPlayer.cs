
// A U# transcription of the VRC example video player graph with more features such as ownership transfer, master lock, video seeking, volume control, and pausing
// Original graph script written by TCL

using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UnityEditorInternal;
using UdonSharpEditor;
using System.Collections.Generic;
#endif

namespace UdonSharp.Video
{
    [AddComponentMenu("Udon Sharp/Video/M Video Player")]
    public class USharpVideoPlayer : UdonSharpBehaviour
    {
        public VRCUrlInputField inputField;
        
        public BaseVRCVideoPlayer videoPlayer;

        [Tooltip("Whether to allow video seeking with the progress bar on the video")]
        public bool allowSeeking = true;
        
        [Tooltip("How often the video player should check if it is more than Sync Threshold out of sync with the video time")]
        public float syncFrequency = 5.0f;
        [Tooltip("How many seconds desynced from the owner the client needs to be to trigger a resync")]
        public float syncThreshold = 1f;
        
        [Tooltip("This list of videos plays sequentially on world load until someone puts in a video")]
        public VRCUrl[] playlist;
        
        public Text urlText;
        public Text urlPlaceholderText;
        public GameObject masterLockedIcon;
        public GameObject masterUnlockedIcon;
        public GameObject pauseIcon;
        public GameObject playIcon;
        public Text statusText;
        public Text statusTextDropShadow;
        public Slider videoProgressSlider;
        public Graphic lockGraphic;

        // Info panel elements
        public Text masterTextField;
        public Text videoOwnerTextField;
        public InputField currentVideoField;
        public InputField lastVideoField;
        public GameObject masterCheckObj;

        [UdonSynced]
        VRCUrl _syncedURL;

        [UdonSynced]
        int _videoNumber;
        int _loadedVideoNumber;

        BaseVRCVideoPlayer _currentPlayer;

        [UdonSynced]
        bool _ownerPlaying;
        [UdonSynced]
        float _videoStartNetworkTime;
        [UdonSynced]
        bool _ownerPaused = false;
        bool _locallyPaused = false;

        bool _waitForSync;
        float _lastSyncTime;

        [UdonSynced]
        bool _masterOnly = true;
        bool _masterOnlyLocal = true;
        bool _needsOwnerTransition = false;

        [UdonSynced]
        int _nextPlaylistIndex = 0;

        string _statusStr = "";

        const int MAX_RETRY_COUNT = 1;
        const float RETRY_TIMEOUT = 10f;

        bool _loadingVideo = false;
        float _currentLoadingTime = 0f;
        int _currentRetryCount = 0;

        private void Start()
        {
            videoPlayer.Loop = false;
            _currentPlayer = videoPlayer;

            PlayNextVideoFromPlaylist();
#if !UNITY_EDITOR // Causes null ref exceptions so just exclude it from the editor
            masterTextField.text = Networking.GetOwner(masterCheckObj).displayName;
#endif
        }

        void StartVideoLoad(VRCUrl url)
        {
            _currentPlayer.LoadURL(url);
            _statusStr = "Loading video...";
            SetStatusText(_statusStr);
            _loadingVideo = true;
            _currentLoadingTime = 0f;
            _currentRetryCount = 0;
        }

        void PlayVideo(VRCUrl url, bool disablePlaylist)
        {
            bool isOwner = Networking.IsOwner(gameObject);

            if (!isOwner && !Networking.IsMaster && _masterOnly)
                return;
            
            if (_syncedURL != null && url.Get() == "")
                return;

            if (!isOwner)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            if (disablePlaylist)
            {
                // -1 means we have stopped using the playlist since we had manual input
                _nextPlaylistIndex = -1;
            }

            StopVideo();

            _syncedURL = url;
            inputField.SetUrl(VRCUrl.Empty);

            if (isOwner)
                _videoNumber++;
            else // Add two to avoid having conflicts where the old owner increases the count
                _videoNumber += 2;

            _loadedVideoNumber = _videoNumber;
            StartVideoLoad(_syncedURL);
            _currentPlayer.Stop();
            _ownerPlaying = false;
            _locallyPaused = _ownerPaused = false;

            _videoStartNetworkTime = float.MaxValue;

            Debug.Log("[USharpVideo] Video URL Changed to " + _syncedURL);
        }

        public void HandleURLInput()
        {
            PlayVideo(inputField.GetUrl(), true);
        }

        void PlayNextVideoFromPlaylist()
        {
            if (_nextPlaylistIndex == -1 || playlist.Length == 0 || !Networking.IsOwner(gameObject))
                return;

            int currentIdx = _nextPlaylistIndex++;

            if (currentIdx >= playlist.Length)
            {
                // We reached the end of the playlist
                _nextPlaylistIndex = -1;
                return;
            }

            PlayVideo(playlist[currentIdx], false);
        }

        public void TriggerLockButton()
        {
            if (!Networking.IsMaster)
                return;

            _masterOnly = _masterOnlyLocal = !_masterOnlyLocal;

            if (_masterOnly && !Networking.IsOwner(gameObject))
            {
                _needsOwnerTransition = true;
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            masterLockedIcon.SetActive(_masterOnly);
            masterUnlockedIcon.SetActive(!_masterOnly);
        }

        public void TriggerPauseButton()
        {
            if (!Networking.IsOwner(gameObject))
                return;

            _ownerPaused = !_ownerPaused;

            if (_ownerPaused)
            {
                _currentPlayer.Pause();
                _locallyPaused = true;
            }
            else
                _currentPlayer.Play();

            playIcon.SetActive(_ownerPaused);
            pauseIcon.SetActive(!_ownerPaused);
        }

        bool _draggingSlider = false;

        // Called from the progress bar slider
        public void OnBeginDrag()
        {
            _draggingSlider = true;
        }

        public void OnEndDrag()
        {
            _draggingSlider = false;
        }

        public void OnSliderChanged()
        {
            if (!_draggingSlider || !allowSeeking)
                return;

            if (!Networking.IsOwner(gameObject))
                return;

            float newSliderValue = videoProgressSlider.value;
            float newTargetTime = _currentPlayer.GetDuration() * newSliderValue;

            _videoStartNetworkTime = (float)Networking.GetServerTimeInSeconds() - newTargetTime;

            SyncVideo();
        }

        // Stop video button
        void StopVideo()
        {
            if (!Networking.IsOwner(gameObject))
                return;

            _videoStartNetworkTime = 0f;
            _ownerPlaying = false;
            _currentPlayer.Stop();
            _syncedURL = VRCUrl.Empty;
            _locallyPaused = _ownerPaused = false;
        }

        public override void OnVideoReady()
        {
            _loadingVideo = false;
            _currentLoadingTime = 0f;
            _currentRetryCount = 0;

            if (Networking.IsOwner(gameObject)) // The owner plays the video when it is ready
            {
                _currentPlayer.Play();
            }
            else // If the owner is playing the video, Play it and run SyncVideo
            {
                if (_ownerPlaying)
                {
                    _currentPlayer.Play();
                    SyncVideo();
                }
                else
                {
                    _waitForSync = true;
                }
            }
        }

        public override void OnVideoStart()
        {
            if (Networking.IsOwner(gameObject))
            {
                if (_locallyPaused)
                    _videoStartNetworkTime = (float)Networking.GetServerTimeInSeconds() - _currentPlayer.GetTime();
                else
                    _videoStartNetworkTime = (float)Networking.GetServerTimeInSeconds();

                _ownerPaused = _locallyPaused = false;
                _ownerPlaying = true;
            }
            else if (!_ownerPlaying) // Watchers pause and wait for sync from owner
            {
                _currentPlayer.Pause();
                _waitForSync = true;
            }

            _statusStr = "";

            lastVideoField.text = currentVideoField.text;
            currentVideoField.text = _syncedURL.Get();

#if !UNITY_EDITOR // Causes null ref exceptions so just exclude it from the editor
            videoOwnerTextField.text = Networking.GetOwner(gameObject).displayName;
#endif
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            masterTextField.text = Networking.GetOwner(masterCheckObj).displayName;
        }

        public override void OnVideoEnd()
        {
            // When the video ends on Owner, set time to 0 and playing to false
            if (Networking.IsOwner(gameObject))
            {
                _videoStartNetworkTime = 0f;
                _ownerPlaying = false;
            }

            PlayNextVideoFromPlaylist();
        }

        public override void OnVideoError()
        {
            _loadingVideo = false;
            _currentLoadingTime = 0f;
            _currentRetryCount = 0;

            _currentPlayer.Stop();
            Debug.LogError("[USharpVideo] Video failed: " + _syncedURL);

            _statusStr = "Failed to load video";
            SetStatusText(_statusStr);
            PlayNextVideoFromPlaylist();
        }

        void UpdateVideoLoad()
        {
            if (_loadingVideo)
            {
                _currentLoadingTime += Time.deltaTime;

                if (_currentLoadingTime > RETRY_TIMEOUT)
                {
                    _currentLoadingTime = 0f;

                    if (++_currentRetryCount > MAX_RETRY_COUNT)
                    {
                        OnVideoError();
                    }
                    else
                    {
                        _currentPlayer.LoadURL(_syncedURL);
                    }
                }
            }
        }

        int _deserializeCounter;

        public override void OnDeserialization()
        {
            // Load new video when _videoNumber is changed
            if (Networking.IsOwner(gameObject))
                return;

            masterLockedIcon.SetActive(_masterOnly);
            masterUnlockedIcon.SetActive(!_masterOnly);
            playIcon.SetActive(_ownerPaused);
            pauseIcon.SetActive(!_ownerPaused);

            // Needed to prevent "rewinding" behaviour of Udon synced strings/VRCUrl's where, when switching ownership the string will be populated with the second to last value locally observed.
            if (_deserializeCounter < 10)
            {
                _deserializeCounter++;
                return;
            }

            if (_locallyPaused)
            {
                _currentPlayer.Play();
                _locallyPaused = false;
            }

            //if (_syncedURL != null)
            //    Debug.Log("[USharpVideo] Received URL " + _syncedURL);

            if (_videoNumber == _loadedVideoNumber)
            {
                return;
            }

            _currentPlayer.Stop();
            StartVideoLoad(_syncedURL);

            SyncVideo();

            _loadedVideoNumber = _videoNumber;

            Debug.Log("[USharpVideo] Playing synced " + _syncedURL);
        }

        public override void OnPreSerialization()
        {
            _deserializeCounter = 0;
        }

        Color redGraphicColor = new Color(0.632f, 0.19f, 0.19f);
        Color whiteGraphicColor = new Color(0.9433f, 0.9433f, 0.9433f);

        private void Update()
        {
            bool isOwner = Networking.IsOwner(gameObject);

            // These need to be moved to OnOwnershipTransferred when it's fixed.
            if (_masterOnly && !Networking.IsMaster)
            {
                urlPlaceholderText.text = $"Only the master {Networking.GetOwner(gameObject).displayName} may add URLs";
                inputField.readOnly = true;
                lockGraphic.color = redGraphicColor;
            }
            else if (!_masterOnly)
            {
                urlPlaceholderText.text = "Enter Video URL... (anyone)";
                inputField.readOnly = false;
                lockGraphic.color = whiteGraphicColor;
            }
            else
            {
                urlPlaceholderText.text = "Enter Video URL...";
                inputField.readOnly = false;

                if (isOwner)
                    lockGraphic.color = whiteGraphicColor;
                else
                    lockGraphic.color = redGraphicColor;
            }

            float currentTime = _currentPlayer.GetTime();
            float duration = _currentPlayer.GetDuration();
            string totalTimeStr = System.TimeSpan.FromSeconds(duration).ToString(@"hh\:mm\:ss");

            if (_draggingSlider && string.IsNullOrEmpty(_statusStr))
            {
                string currentTimeStr = System.TimeSpan.FromSeconds(videoProgressSlider.value * duration).ToString(@"hh\:mm\:ss");
                SetStatusText(currentTimeStr + "/" + totalTimeStr);
            }
            else
            {
                if (string.IsNullOrEmpty(_statusStr))
                {
                    string currentTimeStr = System.TimeSpan.FromSeconds(currentTime).ToString(@"hh\:mm\:ss");
                    SetStatusText(currentTimeStr + "/" + totalTimeStr);
                }

                videoProgressSlider.value = Mathf.Clamp01(currentTime / (duration > 0f ? duration : 1f));
            }

            // Keep the target time the same while paused
            if (_ownerPaused)
            {
                _videoStartNetworkTime = (float)Networking.GetServerTimeInSeconds() - currentTime;

                _currentPlayer.Pause();
                _locallyPaused = true;
            }
            
            UpdateVideoLoad();

            if (isOwner || !_waitForSync)
            {
                if (isOwner && _needsOwnerTransition)
                {
                    StopVideo();
                    _needsOwnerTransition = false;
                    _masterOnly = _masterOnlyLocal;
                }

                SyncVideoIfTime();

                return;
            }

            if (!_ownerPlaying)
                return;

            _currentPlayer.Play();

            _waitForSync = false;

            SyncVideo();
        }

        void SyncVideoIfTime()
        {
            if (Time.realtimeSinceStartup - _lastSyncTime > syncFrequency)
            {
                _lastSyncTime = Time.realtimeSinceStartup;
                SyncVideo();
            }
        }

        void SyncVideo()
        {
            float offsetTime = Mathf.Clamp((float)Networking.GetServerTimeInSeconds() - _videoStartNetworkTime, 0f, _currentPlayer.GetDuration());

            if (Mathf.Abs(_currentPlayer.GetTime() - offsetTime) > syncThreshold)
            {
                _currentPlayer.SetTime(offsetTime);
                //Debug.LogFormat("[USharpVideo] Syncing Video to {0:N2}", offsetTime);
            }
        }

        void SetStatusText(string value)
        {
            statusText.text = value;
            statusTextDropShadow.text = value;
        }
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    [CustomEditor(typeof(USharpVideoPlayer))]
    internal class USharpVideoPlayerInspector : Editor
    {
        static bool _showUIReferencesDropdown = false;

        SerializedProperty videoPlayerProperty;

        ReorderableList playlistList;

        SerializedProperty allowSeekProperty;
        SerializedProperty syncFrequencyProperty;
        SerializedProperty syncThresholdProperty;
        SerializedProperty playlistProperty;

        // UI fields
        SerializedProperty urlTextProperty;
        SerializedProperty urlPlaceholderTextProperty;
        SerializedProperty masterLockedIconProperty;
        SerializedProperty masterUnlockedIconProperty;
        SerializedProperty pauseIconProperty;
        SerializedProperty playIconProperty;
        SerializedProperty statusTextProperty;
        SerializedProperty statusTextDropShadowProperty;
        SerializedProperty videoProgressSlider;
        SerializedProperty lockGraphicProperty;

        // Info panel fields
        SerializedProperty masterTextFieldProperty;
        SerializedProperty videoOwnerTextFieldProperty;
        SerializedProperty currentVideoFieldProperty;
        SerializedProperty lastVideoFieldProperty;
        SerializedProperty masterCheckObjProperty;

        private void OnEnable()
        {
            videoPlayerProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.videoPlayer));

            allowSeekProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.allowSeeking));
            syncFrequencyProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.syncFrequency));
            syncThresholdProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.syncThreshold));

            playlistProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.playlist));

            // UI Fields
            urlTextProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.urlText));
            urlPlaceholderTextProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.urlPlaceholderText));
            masterLockedIconProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.masterLockedIcon));
            masterUnlockedIconProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.masterUnlockedIcon));
            pauseIconProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.pauseIcon));
            playIconProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.playIcon));
            statusTextProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.statusText));
            statusTextDropShadowProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.statusTextDropShadow));
            videoProgressSlider = serializedObject.FindProperty(nameof(USharpVideoPlayer.videoProgressSlider));
            lockGraphicProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.lockGraphic));

            masterTextFieldProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.masterTextField));
            videoOwnerTextFieldProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.videoOwnerTextField));
            currentVideoFieldProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.currentVideoField));
            lastVideoFieldProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.lastVideoField));
            masterCheckObjProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.masterCheckObj));

            // Playlist
            playlistList = new ReorderableList(serializedObject, playlistProperty, true, true, true, true);
            playlistList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                Rect testFieldRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);

                EditorGUI.PropertyField(testFieldRect, playlistList.serializedProperty.GetArrayElementAtIndex(index), label: new GUIContent());
            };
            playlistList.drawHeaderCallback = (Rect rect) => { EditorGUI.LabelField(rect, "Default Playlist URLs"); };
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawConvertToUdonBehaviourButton(target) ||
                UdonSharpGUI.DrawProgramSource(target))
                return;

            EditorGUILayout.PropertyField(videoPlayerProperty);

            EditorGUILayout.PropertyField(allowSeekProperty);
            EditorGUILayout.PropertyField(syncFrequencyProperty);
            EditorGUILayout.PropertyField(syncThresholdProperty);

            EditorGUILayout.Space();
            playlistList.DoLayoutList();

            EditorGUILayout.Space();
            _showUIReferencesDropdown = EditorGUILayout.Foldout(_showUIReferencesDropdown, "UI Element References");

            if (_showUIReferencesDropdown)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(urlTextProperty);
                EditorGUILayout.PropertyField(urlPlaceholderTextProperty);
                EditorGUILayout.PropertyField(masterLockedIconProperty);
                EditorGUILayout.PropertyField(masterUnlockedIconProperty);
                EditorGUILayout.PropertyField(pauseIconProperty);
                EditorGUILayout.PropertyField(playIconProperty);
                EditorGUILayout.PropertyField(statusTextProperty);
                EditorGUILayout.PropertyField(statusTextDropShadowProperty);
                EditorGUILayout.PropertyField(videoProgressSlider);
                EditorGUILayout.PropertyField(lockGraphicProperty);

                EditorGUILayout.PropertyField(masterTextFieldProperty);
                EditorGUILayout.PropertyField(videoOwnerTextFieldProperty);
                EditorGUILayout.PropertyField(currentVideoFieldProperty);
                EditorGUILayout.PropertyField(lastVideoFieldProperty);
                EditorGUILayout.PropertyField(masterCheckObjProperty);

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
