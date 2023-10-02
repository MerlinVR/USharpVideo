
#define USE_SERVER_TIME_MS // Uses GetServerTimeMilliseconds instead of the server datetime which in theory is less reliable

using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace UdonSharp.Video
{
    [AddComponentMenu("Udon Sharp/Video/USharp Video Player")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class USharpVideoPlayer : UdonSharpBehaviour
    {
        // Video player references
        private VideoPlayerManager _videoPlayerManager;

        [Tooltip("Whether to allow video seeking with the progress bar on the video")]
        [PublicAPI]
        public bool allowSeeking = true;
        
        [Tooltip("If enabled defaults to unlocked so anyone can put in a URL")]
        [SerializeField]
        private bool defaultUnlocked = true;

        [Tooltip("If enabled allows the instance creator to always control the video player regardless of if they are master or not")]
        [PublicAPI]
        public bool allowInstanceCreatorControl = true;
        
        [Tooltip("How often the video player should check if it is more than Sync Threshold out of sync with the video time")]
        [PublicAPI]
        public float syncFrequency = 8.0f;
        [Tooltip("How many seconds desynced from the owner the client needs to be to trigger a resync")]
        [PublicAPI]
        public float syncThreshold = 0.85f;
        
        [Range(0f, 1f)]
        [Tooltip("The default volume for the volume slider on the video player")]
        [SerializeField]
        private float defaultVolume = 0.5f;

#pragma warning disable CS0414
        [Tooltip("The max range of the audio sources on this video player")]
        [SerializeField]
        private float audioRange = 40f;
#pragma warning restore CS0414

        /// <summary>
        /// Local offset from the network time to sync the video
        /// Can be used for things like making a video player sync up with someone singing
        /// </summary>
        [PublicAPI, System.NonSerialized]
        public float localSyncOffset;
        
        [Tooltip("List of urls to play automatically when the world is loaded until someone puts in another URL")]
        public VRCUrl[] playlist = new VRCUrl[0];

        [Tooltip("Should default to the stream player? This is usually used when you want to put a live stream in the default playlist.")]
        [SerializeField]
        private bool defaultStreamMode;

        [Tooltip("If the default playlist should loop")]
        [PublicAPI]
        public bool loopPlaylist;

        [Tooltip("If the default playlist should be shuffled upon world load")]
        public bool shufflePlaylist;

        /// <summary>
        /// The URL that we should currently be playing and that other people are playing
        /// </summary>
        [UdonSynced]
        private VRCUrl _syncedURL = VRCUrl.Empty;

        /// <summary>
        /// The video sequence identifier, gets incremented whenever a new video is put in. Used to determine in combination with _currentVideoIdx if we need to load the new URL
        /// </summary>
        [UdonSynced]
        private int _syncedVideoIdx;
        private int _currentVideoIdx;

        /// <summary>
        /// If we're locked so only the master may put in URLs
        /// </summary>
        [UdonSynced]
        private bool _isMasterOnly = true;

        [UdonSynced]
        private int _nextPlaylistIndex;

#if USE_SERVER_TIME_MS
        [UdonSynced]
        private int _networkTimeVideoStart;
        private int _localNetworkTimeStart;
#else
        [UdonSynced]
        private double _videoStartNetworkTime;
        private double _localVideoStartTime;

        [UdonSynced]
        private long _networkTimeStart;
        private System.DateTime _localNetworkTimeStart;
#endif

        [UdonSynced]
        private bool _ownerPlaying;

        [UdonSynced]
        private bool _ownerPaused;
        private bool _locallyPaused;

        [UdonSynced]
        private bool _loopVideo;
        private bool _localLoopVideo;

        [UdonSynced]
        private int _shuffleSeed;

        // The last unpaused time in the video
        private float _lastVideoTime;

        VideoControlHandler[] _registeredControlHandlers;
        VideoScreenHandler[] _registeredScreenHandlers;
        UdonSharpBehaviour[] _registeredCallbackReceivers;

        // Video loading state
        const int MAX_RETRY_COUNT = 4;
        const float DEFAULT_RETRY_TIMEOUT = 35.0f;
        const float RATE_LIMIT_RETRY_TIMEOUT = 5.5f;
        const float VIDEO_ERROR_RETRY_TIMEOUT = 5f;
        const float PLAYLIST_ERROR_RETRY_COUNT = 4;

        private bool _loadingVideo;
        private float _currentLoadingTime; // Counts down to 0 while loading
        private int _currentRetryCount;
        private float _videoTargetStartTime;
        private int _playlistErrorCount;

        private bool _waitForSync;

        // Player mode tracking
        const int PLAYER_MODE_UNITY = 0;
        const int PLAYER_MODE_AVPRO = 1;

        [UdonSynced]
        private int currentPlayerMode = PLAYER_MODE_UNITY;
        private int _localPlayerMode = PLAYER_MODE_UNITY;

        private bool _videoSync = true;

        private bool _ranInit;

        private void Start()
        {
            if (_ranInit)
                return;

            _ranInit = true;

            _videoPlayerManager = GetVideoManager();
            _videoPlayerManager.Start();

            if (_registeredControlHandlers == null)
                _registeredControlHandlers = new VideoControlHandler[0];

            if (_registeredCallbackReceivers == null)
                _registeredCallbackReceivers = new UdonSharpBehaviour[0];

            if (Networking.IsOwner(gameObject))
            {
                if (defaultUnlocked)
                    _isMasterOnly = false;

                if (defaultStreamMode)
                {
                    SetPlayerMode(PLAYER_MODE_AVPRO);
                    _nextPlaylistIndex = 0; // SetPlayerMode sets this to -1, but we want to be able to keep it intact so reset to 0
                }

                _shuffleSeed = Random.Range(0, 10000);
            }

            _lastMasterLocked = _isMasterOnly;

            SetUILocked(_isMasterOnly);

#if !USE_SERVER_TIME_MS
            _networkTimeStart = Networking.GetNetworkDateTime().Ticks;
            _localNetworkTimeStart = new System.DateTime(_networkTimeStart, System.DateTimeKind.Utc);
#endif

            PlayNextVideoFromPlaylist();

            SetVolume(defaultVolume);

            // Serialize the default setup state from the master once regardless of if a video has played
            QueueSerialize();
            
            LogMessage("USharpVideo v1.0.1 Initialized");
        }

        public override void OnVideoReady()
        {
            ResetVideoLoad();
            _playlistErrorCount = 0;

            if (IsUsingAVProPlayer())
            {
                float duration = _videoPlayerManager.GetDuration();

                if (duration == float.MaxValue || float.IsInfinity(duration) || IsRTSPStream())
                    _videoSync = false;
                else
                    _videoSync = true;
            }
            else
                _videoSync = true;

            if (_videoSync)
            {
                if (Networking.IsOwner(gameObject))
                {
                    _waitForSync = false;
                    _videoPlayerManager.Play();
                }
                else
                {
                    if (_ownerPlaying)
                    {
                        _waitForSync = false;
                        _locallyPaused = false;
                        _videoPlayerManager.Play();

                        SyncVideo();
                    }
                    else
                    {
#if USE_SERVER_TIME_MS
                        if (_networkTimeVideoStart == 0)
#else
                        if (_videoStartNetworkTime == 0f || _videoStartNetworkTime > GetNetworkTime() - _videoPlayerManager.GetDuration()) // Todo: remove the 0f check and see how this actually gets set to 0 while the owner is playing
#endif
                        {
                            _waitForSync = true;
                            SetStatusText("Waiting for owner sync...");
                        }
                        else
                        {
                            _waitForSync = false;
                            SyncVideo();
                            SetStatusText("");
#if USE_SERVER_TIME_MS
                            LogMessage($"Loaded into world with complete video, duration: {_videoPlayerManager.GetDuration()}, start net time: {_networkTimeVideoStart}");
#else
                            LogMessage($"Loaded into world with complete video, duration: {_videoPlayerManager.GetDuration()}, start net time: {_videoStartNetworkTime}, subtracted net time {GetNetworkTime() - _videoPlayerManager.GetDuration()}");
#endif
                        }
                    }
                }
            }
            else // Live streams should start asap
            {
                _waitForSync = false;
                _videoPlayerManager.Play();
            }
        }

        public override void OnVideoStart()
        {
            if (Networking.IsOwner(gameObject))
            {
                SetPausedInternal(false, false);

#if USE_SERVER_TIME_MS
                _networkTimeVideoStart = Networking.GetServerTimeInMilliseconds() - (int)(_videoTargetStartTime * 1000f);
#else
                _videoStartNetworkTime = GetNetworkTime() - _videoTargetStartTime;
#endif
    
                if (IsInVideoMode())
                {
                    _videoPlayerManager.SetTime(_videoTargetStartTime);
                }

                _ownerPlaying = true;

                QueueSerialize();

                LogMessage($"Started video: {_syncedURL}");
            }
            else if (!_ownerPlaying) // Watchers pause and wait for sync from owner
            {
                _videoPlayerManager.Pause();
                _waitForSync = true;
            }
            else
            {
                SetPausedInternal(_ownerPaused, false);
                SyncVideo();
                LogMessage($"Started video: {_syncedURL}");
            }

            SetStatusText("");

            _videoTargetStartTime = 0f;

            SetUIPaused(_locallyPaused);

            UpdateRenderTexture();

            SendCallback("OnUSharpVideoPlay");
        }

        private bool IsRTSPStream()
        {
            string urlStr = _syncedURL.ToString();

            return IsUsingAVProPlayer() &&
                   _videoPlayerManager.GetDuration() == 0f && 
                   IsRTSPURL(urlStr);
        }

        private bool IsRTSPURL(string urlStr)
        {
            return urlStr.StartsWith("rtsp://", System.StringComparison.OrdinalIgnoreCase) ||
                   urlStr.StartsWith("rtmp://", System.StringComparison.OrdinalIgnoreCase) || // RTMP isn't really supported in VRC's context and it's probably never going to be, but we'll just be safe here
                   urlStr.StartsWith("rtspt://", System.StringComparison.OrdinalIgnoreCase) || // rtsp over TCP
                   urlStr.StartsWith("rtspu://", System.StringComparison.OrdinalIgnoreCase); // rtsp over UDP
        }

        public override void OnVideoEnd()
        {
            // VRC falsely throws OnVideoEnd instantly on RTSP streams since they report 0 length
            if (IsRTSPStream()) 
                return;
            
            if (Networking.IsOwner(gameObject))
            {
                _ownerPlaying = false;
                _ownerPaused = _locallyPaused = false;

                SetStatusText("");
                SetUIPaused(false);

                PlayNextVideoFromPlaylist();
                QueueSerialize();
            }

            SendCallback("OnUSharpVideoEnd");

            UpdateRenderTexture();
        }

        // Workaround for bug that needs to be addressed in U# where calling built in methods with parameters will get the parameters overwritten when called from other UdonBehaviours
        public void _OnVideoErrorCallback(VideoError videoError)
        {
            OnVideoError(videoError);
        }

        public override void OnVideoError(VideoError videoError)
        {
            if (videoError == VideoError.RateLimited)
            {
                SetStatusText("Rate limited, retrying...");
                LogWarning("Rate limited, retrying...");
                _currentLoadingTime = RATE_LIMIT_RETRY_TIMEOUT;
                return;
            }
            
            if (videoError == VideoError.PlayerError)
            {
                SetStatusText("Video error, retrying...");
                LogError("Video player error when trying to load " + _syncedURL);
                _loadingVideo = true; // Apparently OnVideoReady gets fired erroneously??
                _currentLoadingTime = VIDEO_ERROR_RETRY_TIMEOUT;
                return;
            }

            ResetVideoLoad();
            _videoTargetStartTime = 0f;

            _videoPlayerManager.Stop();

            LogError($"Video '{_syncedURL}' failed to play with error {videoError}");

            switch (videoError)
            {
                case VideoError.InvalidURL:
                    SetStatusText("Invalid URL");
                    break;
                case VideoError.AccessDenied:
                    SetStatusText("Video blocked, enable untrusted URLs");
                    break;
                default:
                    SetStatusText("Failed to load video");
                    break;
            }

            ++_playlistErrorCount;
            PlayNextVideoFromPlaylist();

            SendCallback("OnUSharpVideoError");
        }

        public override void OnVideoPause() { }
        public override void OnVideoPlay() { }

        public override void OnVideoLoop()
        {
#if USE_SERVER_TIME_MS
            _localNetworkTimeStart = _networkTimeVideoStart = Networking.GetServerTimeInMilliseconds();
#else
            _localVideoStartTime = _videoStartNetworkTime = GetNetworkTime();
#endif

            QueueSerialize();
        }

        private float _lastCurrentTime;

        private void Update()
        {
            if (_loadingVideo)
                UpdateVideoLoad();

            if (_locallyPaused)
            {
                if (IsInVideoMode())
                {
                    // Keep the target time the same while paused
#if USE_SERVER_TIME_MS
                    _networkTimeVideoStart = Networking.GetServerTimeInMilliseconds() - (int)(_videoPlayerManager.GetTime() * 1000f);
#else
                    _videoStartNetworkTime = GetNetworkTime() - _videoPlayerManager.GetTime();
#endif
                }
            }
            else
                _lastCurrentTime = _videoPlayerManager.GetTime();

            if (Networking.IsOwner(gameObject) || !_waitForSync)
            {
                SyncVideoIfTime();
            }
            else if (_ownerPlaying)
            {
                _videoPlayerManager.Play();
                LogMessage($"Started video: {_syncedURL}");
                _waitForSync = false;
                SyncVideo();
            }
            
            UpdateRenderTexture(); // Needed because AVPro can swap textures whenever
        }

        /// <summary>
        /// Uncomment this to prevent people from taking ownership of the video player when they shouldn't be able to
        /// </summary>
        //public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        //{
        //    return !_isMasterOnly || IsPrivlegedUser(requestedOwner);
        //}
        
        private bool _lastMasterLocked;

        public override void OnDeserialization()
        {
            if (Networking.IsOwner(gameObject))
                return;

#if !USE_SERVER_TIME_MS
            _localNetworkTimeStart = new System.DateTime(_networkTimeStart, System.DateTimeKind.Utc);
#endif

            SetPausedInternal(_ownerPaused, false);
            SetLoopingInternal(_loopVideo);

            if (_localPlayerMode != currentPlayerMode)
                SetPlayerMode(currentPlayerMode);

            if (_isMasterOnly != _lastMasterLocked)
                SetLockedInternal(_isMasterOnly);
            
            if (!_ownerPlaying && _videoPlayerManager.IsPlaying())
                _videoPlayerManager.Stop();

            if (_currentVideoIdx != _syncedVideoIdx)
            {
                _currentVideoIdx = _syncedVideoIdx;

                _videoPlayerManager.Stop();
                StartVideoLoad(_syncedURL);

#if USE_SERVER_TIME_MS
                _localNetworkTimeStart = _networkTimeVideoStart;
#else
                _localVideoStartTime = _videoStartNetworkTime;
#endif

                LogMessage("Playing synced " + _syncedURL);
            }
#if USE_SERVER_TIME_MS
            else if (_networkTimeVideoStart != _localNetworkTimeStart) // Detect seeks
            {
                _localNetworkTimeStart = _networkTimeVideoStart;
#else
            else if (_videoStartNetworkTime != _localVideoStartTime) // Detect seeks
            {
                _localVideoStartTime = _videoStartNetworkTime;
#endif
                SyncVideo();
            }

            if (!_locallyPaused && IsInVideoMode())
            {
                float duration = GetVideoManager().GetDuration();

                // If the owner did a seek on the video after it finished, we need to start playing it again
#if USE_SERVER_TIME_MS
                if ((Networking.GetServerTimeInMilliseconds() - _networkTimeVideoStart) / 1000f < duration - 3f)
#else
                if (GetNetworkTime() - _videoStartNetworkTime < duration - 3f)
#endif
                    _videoPlayerManager.Play();
            }

            SendCallback("OnUSharpVideoDeserialization");
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            SendUIOwnerUpdate();

            SendCallback("OnUSharpVideoOwnershipChange");
        }

        // Supposedly there's some case where late joiners don't receive data, so do a serialization just in case here.
        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (!player.isLocal)
                QueueSerialize();
        }

        /// <summary>
        /// Stops playback of the video completely and clears data
        /// </summary>
        [PublicAPI]
        public void StopVideo()
        {
            if (!Networking.IsOwner(gameObject))
                return;

#if USE_SERVER_TIME_MS
            _networkTimeVideoStart = 0;
#else
            _videoStartNetworkTime = 0f;
#endif
            _ownerPlaying = false;
            _locallyPaused = _ownerPaused = false;
            _videoTargetStartTime = 0f;
            _lastCurrentTime = 0f;

            _videoPlayerManager.Stop();
            SetUIPaused(false);
            ResetVideoLoad();

            QueueSerialize();

            SendCallback("OnUSharpVideoStop");
        }

        /// <summary>
        /// Play a video with the specified URL, only works if the player is allowed to use the video player
        /// </summary>
        /// <param name="url"></param>
        [PublicAPI]
        public void PlayVideo(VRCUrl url)
        {
            PlayVideoInternal(url, true);
        }

        /// <summary>
        /// Returns the URL that the video player currently has loaded
        /// </summary>
        /// <returns></returns>
        [PublicAPI]
        public VRCUrl GetCurrentURL() => _syncedURL;

        private void PlayVideoInternal(VRCUrl url, bool stopPlaylist)
        {
            if (!CanControlVideoPlayer())
                return;

            string urlStr = url.Get();

            if (!ValidateURL(urlStr))
                return;

            bool wasOwner = Networking.IsOwner(gameObject);

            TakeOwnership();

            if (stopPlaylist)
                _nextPlaylistIndex = -1;
            
            StopVideo();

            _syncedURL = url;

            if (wasOwner)
                ++_syncedVideoIdx;
            else // Add two to avoid having conflicts where the old owner increases the count
                _syncedVideoIdx += 2;

            _currentVideoIdx = _syncedVideoIdx;
            
            StartVideoLoad(url);
            _ownerPlaying = false;
#if USE_SERVER_TIME_MS
            _networkTimeVideoStart = 0;
#endif

            _videoTargetStartTime = GetVideoStartTime(urlStr);

            QueueSerialize();

            SendCallback("OnUSharpVideoLoadStart");
        }

        private void ResetVideoLoad()
        {
            _loadingVideo = false;
            _currentRetryCount = 0;
            _currentLoadingTime = DEFAULT_RETRY_TIMEOUT;
        }

        private void UpdateVideoLoad()
        {
            //if (_loadingVideo) // Checked in caller now since it's cheaper
            {
                _currentLoadingTime -= Time.deltaTime;

                if (_currentLoadingTime <= 0f)
                {
                    _currentLoadingTime = DEFAULT_RETRY_TIMEOUT;

                    if (++_currentRetryCount > MAX_RETRY_COUNT)
                    {
                        OnVideoError(VideoError.Unknown);
                    }
                    else
                    {
                        LogMessage("Retrying load");

                        SetStatusText("Retrying load...");
                        _videoPlayerManager.LoadURL(_syncedURL);
                    }
                }
            }
        }

        private float _lastSyncTime;

        private void SyncVideoIfTime()
        {
            float timeSinceStartup = Time.realtimeSinceStartup;

            if (timeSinceStartup - _lastSyncTime > syncFrequency)
            {
                _lastSyncTime = timeSinceStartup;
                SyncVideo();
            }
        }

        /// <summary>
        /// Syncs the video time if it's too far diverged from the network time
        /// </summary>
        [PublicAPI]
        public void SyncVideo()
        {
            if (IsInVideoMode())
            {
#if USE_SERVER_TIME_MS
                float offsetTime = Mathf.Clamp((Networking.GetServerTimeInMilliseconds() - _networkTimeVideoStart) / 1000f + localSyncOffset, 0f, _videoPlayerManager.GetDuration());
#else
                float offsetTime = Mathf.Clamp((float)(GetNetworkTime() - _videoStartNetworkTime) + localSyncOffset, 0f, _videoPlayerManager.GetDuration());
#endif

                if (Mathf.Abs(_videoPlayerManager.GetTime() - offsetTime) > syncThreshold)
                {
                    _videoPlayerManager.SetTime(offsetTime);
                    LogMessage($"Syncing video to {offsetTime:N2}");
                }
            }
        }

        /// <summary>
        /// Syncs the video time regardless of how far diverged it is from the network time, can be used as a less aggressive audio resync in some cases
        /// </summary>
        [PublicAPI]
        public void ForceSyncVideo()
        {
            if (IsInVideoMode())
            {
#if USE_SERVER_TIME_MS
                float offsetTime = Mathf.Clamp((Networking.GetServerTimeInMilliseconds() - _networkTimeVideoStart) / 1000f + localSyncOffset, 0f, _videoPlayerManager.GetDuration());
#else
                float offsetTime = Mathf.Clamp((float)(GetNetworkTime() - _videoStartNetworkTime) + localSyncOffset, 0f, _videoPlayerManager.GetDuration());
#endif

                float syncNudgeTime = Mathf.Max(0f, offsetTime - 1f);
                _videoPlayerManager.SetTime(syncNudgeTime); // Seek to slightly earlier before syncing to the real time to get the video player to jump cleanly
                _videoPlayerManager.SetTime(offsetTime);
                LogMessage($"Syncing video to {offsetTime:N2}");
            }
        }

        private void StartVideoLoad(VRCUrl url)
        {
#if UNITY_EDITOR
            LogMessage($"Started video load for URL: {url}");
#else
            LogMessage($"Started video load for URL: {url}, requested by {Networking.GetOwner(gameObject).displayName}");
#endif

            SetStatusText("Loading video...");
            ResetVideoLoad();
            _loadingVideo = true;
            _videoPlayerManager.LoadURL(url);

            AddUIUrlHistory(url);
        }

        private void SetPausedInternal(bool paused, bool updatePauseTime)
        {
            if (Networking.IsOwner(gameObject))
                _ownerPaused = paused;

            if (_locallyPaused != paused)
            {
                _locallyPaused = paused;

                if (IsInVideoMode())
                {
                    if (_ownerPaused)
                        _videoPlayerManager.Pause();
                    else
                    {
                        _videoPlayerManager.Play();
                        if (updatePauseTime)
                            _videoTargetStartTime = _lastCurrentTime;
                    }
                }
                else
                {
                    if (_ownerPaused)
                        _videoPlayerManager.Stop();
                    else
                        StartVideoLoad(_syncedURL);
                }

                SetUIPaused(paused);

                if (_locallyPaused)
                    SendCallback("OnUSharpVideoPause");
                else
                    SendCallback("OnUSharpVideoUnpause");
            }

            QueueRateLimitedSerialize();
        }

        /// <summary>
        /// Pauses the video if we have control of the video player.
        /// </summary>
        /// <param name="paused"></param>
        [PublicAPI]
        public void SetPaused(bool paused)
        {
            if (Networking.IsOwner(gameObject))
                SetPausedInternal(paused, true);
        }

        [PublicAPI]
        public bool IsPaused()
        {
            return _ownerPaused;
        }

        private void SetLoopingInternal(bool loop)
        {
            if (loop == _localLoopVideo)
                return;

            _loopVideo = _localLoopVideo = loop;

            _videoPlayerManager.SetLooping(loop);

            SetUILooping(loop);

            QueueRateLimitedSerialize();
        }

        /// <summary>
        /// Sets whether the currently playing video should loop and restart once it ends.
        /// </summary>
        /// <param name="loop"></param>
        [PublicAPI]
        public void SetLooping(bool loop)
        {
            if (Networking.IsOwner(gameObject))
                SetLoopingInternal(loop);
        }

        [PublicAPI]
        public bool IsLooping()
        {
            return _localLoopVideo;
        }

        [PublicAPI]
        public float GetVolume() => _videoPlayerManager.GetVolume();

        /// <summary>
        /// Sets the audio source volume for the audio sources used by this video player.
        /// </summary>
        /// <param name="volume"></param>
        [PublicAPI]
        public void SetVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            if (volume == _videoPlayerManager.GetVolume())
                return;

            _videoPlayerManager.SetVolume(volume);
            SetUIVolume(volume);
        }

        [PublicAPI]
        public bool IsMuted() => _videoPlayerManager.IsMuted();

        /// <summary>
        /// Mutes audio from this video player
        /// </summary>
        /// <param name="muted"></param>
        [PublicAPI]
        public void SetMuted(bool muted)
        {
            _videoPlayerManager.SetMuted(muted);
            SetUIMuted(muted);
        }

        private bool _delayedSyncAllowed = true;
        private int _finalSyncCounter;

        /// <summary>
        /// Takes a float in the range 0 to 1 and seeks the video to that % through the time
        /// Is intended to be used with progress bar-type-things
        /// </summary>
        /// <param name="progress"></param>
        [PublicAPI]
        public void SeekTo(float progress)
        {
            if (!allowSeeking || !Networking.IsOwner(gameObject))
                return;

            float newTargetTime = _videoPlayerManager.GetDuration() * progress;
            _lastVideoTime = newTargetTime;
            _lastCurrentTime = newTargetTime;

#if USE_SERVER_TIME_MS
            _localNetworkTimeStart = _networkTimeVideoStart = Networking.GetServerTimeInMilliseconds() - (int)(newTargetTime * 1000f);
#else
            _videoStartNetworkTime = GetNetworkTime() - newTargetTime;
            _localVideoStartTime = _videoStartNetworkTime;
#endif

            if (!_locallyPaused && !GetVideoManager().IsPlaying())
                GetVideoManager().Play();

            SyncVideo();
            QueueRateLimitedSerialize();
        }

        /// <summary>
        /// Used on things that are easily spammable to prevent flooding the network unintentionally.
        /// Will allow 1 sync every half second and then will send a final sync to propagate the final changed values of things at the end
        /// </summary>
        private void QueueRateLimitedSerialize()
        {
            //QueueSerialize(); // Debugging line :D this serialization method can potentially hide some issues so we want to disable it sometimes and verify stuff works right

            if (_delayedSyncAllowed)
            {
                QueueSerialize();
                _delayedSyncAllowed = false;
                SendCustomEventDelayedSeconds(nameof(_UnlockDelayedSync), 0.5f);
            }

            ++_finalSyncCounter;
            SendCustomEventDelayedSeconds(nameof(_SendFinalSync), 0.8f);
        }

        public void _UnlockDelayedSync()
        {
            _delayedSyncAllowed = true;
        }

        // Handles running a final sync after doing a QueueRateLimitedSerialize, so that the final changes of the seek time get propagated
        public void _SendFinalSync()
        {
            if (--_finalSyncCounter == 0)
                QueueSerialize();
        }
        
        /// <summary>
        /// Determines if the local player can control this video player. This means the player is either the master, the instance creator, or the video player is unlocked.
        /// </summary>
        /// <returns></returns>
        [PublicAPI]
        public bool CanControlVideoPlayer()
        {
            return !_isMasterOnly || IsPrivilegedUser(Networking.LocalPlayer);
        }

        /// <summary>
        /// If the given player is allowed to take important actions on this video player such as changing the video or locking the video player.
        /// This is what you would extend if you want to add an access control list or something similar.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        [PublicAPI]
        public bool IsPrivilegedUser(VRCPlayerApi player)
        {
#if UNITY_EDITOR
            if (player == null)
                return true;
#endif

            return player.isMaster || (allowInstanceCreatorControl && player.isInstanceOwner);
        }

        /// <summary>
        /// Takes ownership of the video player if allowed
        /// </summary>
        [PublicAPI]
        public void TakeOwnership()
        {
            if (Networking.IsOwner(gameObject))
                return;

            if (CanControlVideoPlayer())
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        [PublicAPI]
        public void QueueSerialize()
        {
            if (!Networking.IsOwner(gameObject))
                return;

            RequestSerialization();
        }

        private bool _shuffled;

        /// <summary>
        /// Plays the next video from the video player's built-in playlist
        /// </summary>
        [PublicAPI]
        public void PlayNextVideoFromPlaylist()
        {
            if (_nextPlaylistIndex == -1 || playlist.Length == 0 || !Networking.IsOwner(gameObject))
                return;

            if (loopPlaylist && _playlistErrorCount > PLAYLIST_ERROR_RETRY_COUNT)
            {
                LogError("Maximum number of retries for playlist video looping hit. Stopping playlist playback.");
                _nextPlaylistIndex = -1;
                return;
            }

            if (shufflePlaylist && !_shuffled)
            {
                Random.InitState(_shuffleSeed);

                for (int i = 0; i < playlist.Length - 1; ++i)
                {
                    int r = Random.Range(i, playlist.Length);
                    if (i == r) { continue; }
                    VRCUrl flipVal = playlist[r];
                    playlist[r] = playlist[i];
                    playlist[i] = flipVal;
                }

                _shuffled = true;
            }

            int currentIdx = _nextPlaylistIndex++;

            if (currentIdx >= playlist.Length)
            {
                if (loopPlaylist)
                {
                    _nextPlaylistIndex = 1;
                    currentIdx = 0;
                }
                else
                {
                    // We reached the end of the playlist
                    _nextPlaylistIndex = -1;
                    return;
                }
            }

            PlayVideoInternal(playlist[currentIdx], false);
        }

        [PublicAPI]
        public int GetPlaylistIndex() => _nextPlaylistIndex > 0 ? _nextPlaylistIndex - 1 : -1;

        [PublicAPI]
        public void SetNextPlaylistVideo(int nextPlaylistIdx)
        {
            _nextPlaylistIndex = nextPlaylistIdx;
        }

        /// <summary>
        /// Sets whether this video player is locked to the master which means only the master has the ability to put new videos in.
        /// </summary>
        /// <param name="locked"></param>
        [PublicAPI]
        public void SetLocked(bool locked)
        {
            if (Networking.IsOwner(gameObject))
                SetLockedInternal(locked);
        }

        private void SetLockedInternal(bool locked)
        {
            _isMasterOnly = locked;
            _lastMasterLocked = _isMasterOnly;

            SetUILocked(locked);

            QueueSerialize();

            SendCallback("OnUSharpVideoLockChange");
        }

        [PublicAPI]
        public bool IsLocked()
        {
            return _isMasterOnly;
        }

        /// <summary>
        /// Sets the video player to use the Unity video player as a backend
        /// </summary>
        [PublicAPI]
        public void SetToUnityPlayer()
        {
            if (CanControlVideoPlayer())
            {
                TakeOwnership();
                SetPlayerMode(PLAYER_MODE_UNITY);
            }
        }

        /// <summary>
        /// Sets the video player to use AVPro as the backend.
        /// AVPro supports streams so this is aliased in UI as the "Stream" player to avoid confusion
        /// </summary>
        [PublicAPI]
        public void SetToAVProPlayer()
        {
            if (CanControlVideoPlayer())
            {
                TakeOwnership();
                SetPlayerMode(PLAYER_MODE_AVPRO);
            }
        }

        private void SetPlayerMode(int newPlayerMode)
        {
            if (_localPlayerMode == newPlayerMode)
                return;

            StopVideo();

            if (Networking.IsOwner(gameObject))
                _syncedURL = VRCUrl.Empty;

            currentPlayerMode = newPlayerMode;

            _locallyPaused = _ownerPaused = false;

            _nextPlaylistIndex = -1;
            
            _localPlayerMode = newPlayerMode;

            ResetVideoLoad();

            if (IsUsingUnityPlayer())
            {
                _videoPlayerManager.SetToVideoPlayerMode();
                SetUIToVideoMode();
            }
            else
            {
                _videoPlayerManager.SetToStreamPlayerMode();
                SetUIToStreamMode();
            }

            QueueSerialize();

            UpdateRenderTexture();

            SendCallback("OnUSharpVideoModeChange");
        }

        /// <summary>
        /// Are we playing a standard video where we know the length and need to sync its time across clients?
        /// </summary>
        /// <returns></returns>
        [PublicAPI]
        public bool IsInVideoMode()
        {
            return _videoSync;
        }

        /// <summary>
        /// Are we playing some type of live stream where we do not know the length of the stream and do not need to sync time across clients?
        /// </summary>
        /// <returns></returns>
        [PublicAPI]
        public bool IsInStreamMode()
        {
            return !_videoSync;
        }
        
        [PublicAPI]
        public bool IsUsingUnityPlayer()
        {
            return _localPlayerMode == PLAYER_MODE_UNITY;
        }

        [PublicAPI]
        public bool IsUsingAVProPlayer()
        {
            return _localPlayerMode == PLAYER_MODE_AVPRO;
        }

        /// <summary>
        /// Reloads the video on the video player, usually used if the video playback has encountered some internal issue or if the audio has gotten desynced from the video
        /// </summary>
        [PublicAPI]
        public void Reload()
        {
            if ((_ownerPlaying || Networking.IsOwner(gameObject)) && !_loadingVideo)
            {
                StartVideoLoad(_syncedURL);

                if (Networking.IsOwner(gameObject))
                    _videoTargetStartTime = GetVideoManager().GetTime();

                SendCallback("OnUSharpVideoReload");
            }
        }

        public VideoPlayerManager GetVideoManager()
        {
            if (_videoPlayerManager)
                return _videoPlayerManager;

            _videoPlayerManager = GetComponentInChildren<VideoPlayerManager>(true);
            if (_videoPlayerManager == null)
                LogError("Video Player Manager not found, make sure you have a manager setup properly");

            return _videoPlayerManager;
        }

#region Utilities
        /// <summary>
        /// Parses the start time of a YouTube video from the URL.
        /// If no time is found or given URL is not a YouTube URL, returns 0.0
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private float GetVideoStartTime(string url)
        {
            // Attempt to parse out a start time from YouTube links with t= or start=
            if (url.Contains("youtube.com/watch") ||
                url.Contains("youtu.be/"))
            {
                int tIndex = url.IndexOf("?t=", System.StringComparison.Ordinal);
                if (tIndex == -1) tIndex = url.IndexOf("&t=", System.StringComparison.Ordinal);
                if (tIndex == -1) tIndex = url.IndexOf("?start=", System.StringComparison.Ordinal);
                if (tIndex == -1) tIndex = url.IndexOf("&start=", System.StringComparison.Ordinal);

                if (tIndex == -1)
                    return 0f;

                char[] urlArr = url.ToCharArray();
                int numIdx = url.IndexOf('=', tIndex) + 1;

                string intStr = "";

                while (numIdx < urlArr.Length)
                {
                    char currentChar = urlArr[numIdx];
                    if (!char.IsNumber(currentChar))
                        break;

                    intStr += currentChar;

                    ++numIdx;
                }

                if (string.IsNullOrWhiteSpace(intStr))
                    return 0f;

                int secondsCount = 0;
                if (int.TryParse(intStr, out secondsCount))
                    return secondsCount;
            }

            return 0f;
        }

        /// <summary>
        /// Checks for URL sanity and throws warnings if it's not nice.
        /// </summary>
        /// <param name="url"></param>
        private bool ValidateURL(string url)
        {
            if (url.Contains("youtube.com/watch") ||
                url.Contains("youtu.be/"))
            {
                if (url.IndexOf("&list=", System.StringComparison.OrdinalIgnoreCase) != -1)
                    LogWarning($"URL '{url}' input with playlist link, this can slow down YouTubeDL link resolves significantly see: https://vrchat.canny.io/feature-requests/p/add-no-playlist-to-ytdl-arguments-for-url-resolution");
            }
            
            if (string.IsNullOrWhiteSpace(url)) // Don't do anything if the player entered an empty URL by accident
                return false;

            //if (!url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase) &&
            //    !url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) &&
            //    !IsRTSPURL(url))
            int idx = url.IndexOf("://", System.StringComparison.Ordinal);
            if (idx < 1 || idx > 8) // I'm not sure exactly what rule VRC uses so just check for the :// in an expected spot since it seems like VRC checks that it has a protocol at least.
            {
                LogError($"Invalid URL '{url}' provided");
                SetStatusText("Invalid URL");
                SendCustomEventDelayedSeconds(nameof(_LateClearStatusInternal), 2f);
                return false;
            }

            // Longer than most browsers support, see: https://stackoverflow.com/questions/417142/what-is-the-maximum-length-of-a-url-in-different-browsers. I'm not sure if this length will even play in the video player.
            // Most CDN's keep their URLs under 1000 characters so this should be more than reasonable
            // Prevents people from pasting a book and breaking sync on the video player xd
            if (url.Length > 4096)
            {
                LogError($"Video URL is too long! url: '{url}'");
                SetStatusText("Invalid URL");
                SendCustomEventDelayedSeconds(nameof(_LateClearStatusInternal), 2f);
                return false;
            }

            return true;
        }

        public void _LateClearStatusInternal()
        {
            if (_videoPlayerManager.IsPlaying() && !_loadingVideo)
            {
                SetStatusText("");
            }
        }

#if !USE_SERVER_TIME_MS
        /// <summary>
        /// Gets network time with some degree of ms resolution unlike GetServerTimeInSeconds which is 1 second resolution
        /// </summary>
        /// <returns></returns>
        double GetNetworkTime()
        {
            //return Networking.GetServerTimeInSeconds();
            return (Networking.GetNetworkDateTime() - _localNetworkTimeStart).TotalSeconds;
        }
#endif

        private void LogMessage(string message)
        {
            Debug.Log("[<color=#9C6994>USharpVideo</color>] " + message, this);
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning("[<color=#FF00FF>USharpVideo</color>] " + message, this);
        }

        private void LogError(string message)
        {
            Debug.LogError("[<color=#FF00FF>USharpVideo</color>] " + message, this);
        }
#endregion

#region UI Control handling
        public void RegisterControlHandler(VideoControlHandler newControlHandler)
        {
            if (_registeredControlHandlers == null)
                _registeredControlHandlers = new VideoControlHandler[0];

            foreach (VideoControlHandler controlHandler in _registeredControlHandlers)
            {
                if (newControlHandler == controlHandler)
                    return;
            }

            VideoControlHandler[] newControlHandlers = new VideoControlHandler[_registeredControlHandlers.Length + 1];
            _registeredControlHandlers.CopyTo(newControlHandlers, 0);
            _registeredControlHandlers = newControlHandlers;

            _registeredControlHandlers[_registeredControlHandlers.Length - 1] = newControlHandler;

            newControlHandler.SetLocked(_isMasterOnly);
            newControlHandler.SetLooping(_localLoopVideo);
            newControlHandler.SetPaused(_locallyPaused);
            newControlHandler.SetStatusText(_lastStatusText);

            VideoPlayerManager manager = GetVideoManager();
            newControlHandler.SetVolume(manager.GetVolume());
            newControlHandler.SetMuted(manager.IsMuted());
        }
        
        public void UnregisterControlHandler(VideoControlHandler controlHandler)
        {
            if (_registeredControlHandlers == null)
                _registeredControlHandlers = new VideoControlHandler[0];

            int controlHandlerCount = _registeredControlHandlers.Length;
            for (int i = 0; i < controlHandlerCount; ++i)
            {
                VideoControlHandler handler = _registeredControlHandlers[i];

                if (controlHandler == handler)
                {
                    VideoControlHandler[] newControlHandlers = new VideoControlHandler[controlHandlerCount - 1];

                    for (int j = 0; j < i; ++ j)
                        newControlHandlers[j] = _registeredControlHandlers[j];

                    for (int j = i + 1; j < controlHandlerCount; ++j)
                        newControlHandlers[j - 1] = _registeredControlHandlers[j];

                    _registeredControlHandlers = newControlHandlers;

                    return;
                }
            }
        }

        private string _lastStatusText = "";

        private void SetStatusText(string statusText)
        {
            if (statusText == _lastStatusText)
                return;

            _lastStatusText = statusText;

            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetStatusText(statusText);
        }

        private void SetUIPaused(bool paused)
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetPaused(paused);
        }

        private void SetUILocked(bool locked)
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetLocked(locked);
        }

        private void AddUIUrlHistory(VRCUrl url)
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.AddURLToHistory(url);
        }

        private void SetUIToVideoMode()
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetToVideoPlayerMode();
        }

        private void SetUIToStreamMode()
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetToStreamPlayerMode();
        }

        private void SendUIOwnerUpdate()
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.OnVideoPlayerOwnerTransferred();
        }

        private void SetUILooping(bool looping)
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetLooping(looping);
        }

        private void SetUIVolume(float volume)
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetVolume(volume);
        }

        private void SetUIMuted(bool muted)
        {
            foreach (VideoControlHandler handler in _registeredControlHandlers)
                handler.SetMuted(muted);
        }
#endregion

#region Video Screen Handling
        public void RegisterScreenHandler(VideoScreenHandler newScreenHandler)
        {
            if (_registeredScreenHandlers == null)
                _registeredScreenHandlers = new VideoScreenHandler[0];

            foreach (VideoScreenHandler controlHandler in _registeredScreenHandlers)
            {
                if (newScreenHandler == controlHandler)
                    return;
            }

            VideoScreenHandler[] newControlHandlers = new VideoScreenHandler[_registeredScreenHandlers.Length + 1];
            _registeredScreenHandlers.CopyTo(newControlHandlers, 0);
            _registeredScreenHandlers = newControlHandlers;

            _registeredScreenHandlers[_registeredScreenHandlers.Length - 1] = newScreenHandler;
        }

        public void UnregisterScreenHandler(VideoScreenHandler screenHandler)
        {
            if (_registeredScreenHandlers == null)
                _registeredScreenHandlers = new VideoScreenHandler[0];

            int controlHandlerCount = _registeredScreenHandlers.Length;
            for (int i = 0; i < controlHandlerCount; ++i)
            {
                VideoScreenHandler handler = _registeredScreenHandlers[i];

                if (screenHandler == handler)
                {
                    VideoScreenHandler[] newControlHandlers = new VideoScreenHandler[controlHandlerCount - 1];

                    for (int j = 0; j < i; ++j)
                        newControlHandlers[j] = _registeredScreenHandlers[j];

                    for (int j = i + 1; j < controlHandlerCount; ++j)
                        newControlHandlers[j - 1] = _registeredScreenHandlers[j];

                    _registeredScreenHandlers = newControlHandlers;

                    return;
                }
            }
        }

        private Texture _lastAssignedRenderTexture;

        private void UpdateRenderTexture()
        {
            if (_registeredScreenHandlers == null)
                return;

            Texture renderTexture = _videoPlayerManager.GetVideoTexture();

            if (_lastAssignedRenderTexture == renderTexture)
                return;

            foreach (VideoScreenHandler handler in _registeredScreenHandlers)
            {
                if (handler)
                {
                    handler.UpdateVideoTexture(renderTexture, IsUsingAVProPlayer());
                }
            }

            _lastAssignedRenderTexture = renderTexture;

            SendCallback("OnUSharpVideoRenderTextureChange");
        }
#endregion

#region Callback Receivers
        /// <summary>
        /// Registers an UdonSharpBehaviour as a callback receiver for events that happen on this video player.
        /// Callback receivers can be used to react to state changes on the video player without needing to check periodically.
        /// </summary>
        /// <param name="callbackReceiver"></param>
        [PublicAPI]
        public void RegisterCallbackReceiver(UdonSharpBehaviour callbackReceiver)
        {
            if (!callbackReceiver)
                return;

            if (_registeredCallbackReceivers == null)
                _registeredCallbackReceivers = new UdonSharpBehaviour[0];

            foreach (UdonSharpBehaviour currReceiver in _registeredCallbackReceivers)
            {
                if (callbackReceiver == currReceiver)
                    return;
            }

            UdonSharpBehaviour[] newControlHandlers = new UdonSharpBehaviour[_registeredCallbackReceivers.Length + 1];
            _registeredCallbackReceivers.CopyTo(newControlHandlers, 0);
            _registeredCallbackReceivers = newControlHandlers;

            _registeredCallbackReceivers[_registeredCallbackReceivers.Length - 1] = callbackReceiver;
        }

        [PublicAPI]
        public void UnregisterCallbackReceiver(UdonSharpBehaviour callbackReceiver)
        {
            if (!callbackReceiver)
                return;

            if (_registeredCallbackReceivers == null)
                _registeredCallbackReceivers = new UdonSharpBehaviour[0];

            int callbackReceiverCount = _registeredCallbackReceivers.Length;
            for (int i = 0; i < callbackReceiverCount; ++i)
            {
                UdonSharpBehaviour currHandler = _registeredCallbackReceivers[i];

                if (callbackReceiver == currHandler)
                {
                    UdonSharpBehaviour[] newCallbackReceivers = new UdonSharpBehaviour[callbackReceiverCount - 1];

                    for (int j = 0; j < i; ++j)
                        newCallbackReceivers[j] = _registeredCallbackReceivers[j];

                    for (int j = i + 1; j < callbackReceiverCount; ++j)
                        newCallbackReceivers[j - 1] = _registeredCallbackReceivers[j];

                    _registeredCallbackReceivers = newCallbackReceivers;

                    return;
                }
            }
        }

        private void SendCallback(string callbackName)
        {
            foreach (UdonSharpBehaviour callbackReceiver in _registeredCallbackReceivers)
            {
                if (callbackReceiver)
                {
                    callbackReceiver.SendCustomEvent(callbackName);
                }
            }
        }
#endregion
    }
}
