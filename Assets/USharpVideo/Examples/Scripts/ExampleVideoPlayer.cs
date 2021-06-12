
// A U# transcription of the VRC example video player graph
// Original graph script written by TCL

using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    [AddComponentMenu("Udon Sharp/Video/Examples/Example Video Player")]
    public class ExampleVideoPlayer : UdonSharpBehaviour
    {
        public VRCUrlInputField inputField;

        [Header("Video Player References")]
        public VRCUnityVideoPlayer unityVideo;
        public VRCAVProVideoPlayer avProVideo;

        [Header("Sync parameters")]
        public float syncFrequency = 5.0f;
        public float syncThreshold = 1f;

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

        bool _waitForSync;
        float _lastSyncTime;


        private void Start()
        {
            unityVideo.Loop = false;
            _currentPlayer = unityVideo;
        }

        public void HandleURLInput()
        {
            if (!Networking.IsOwner(gameObject))
                return;

            _syncedURL = inputField.GetUrl();
            _videoNumber++;

            _loadedVideoNumber = _videoNumber;

            _currentPlayer.Stop();
            _currentPlayer.LoadURL(_syncedURL);
            _ownerPlaying = false;

            _videoStartNetworkTime = float.MaxValue;

            Debug.Log("Video URL Changed to " + _syncedURL);
        }

        // Stop video button
        public void StopVideo()
        {
            if (!Networking.IsOwner(gameObject))
                return;

            _videoStartNetworkTime = 0f;
            _ownerPlaying = false;
            _currentPlayer.Stop();
            _syncedURL = VRCUrl.Empty;
        }
        
        public override void OnVideoReady()
        {
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
                _videoStartNetworkTime = (float)Networking.GetServerTimeInSeconds();
                _ownerPlaying = true;
            }
            else if (!_ownerPlaying) // Watchers pause and wait for sync from owner
            {
                _currentPlayer.Pause();
                _waitForSync = true;
            }
        }

        public override void OnVideoEnd()
        {
            // When the video ends on Owner, set time to 0 and playing to false
            if (Networking.IsOwner(gameObject))
            {
                _videoStartNetworkTime = 0f;
                _ownerPlaying = false;
            }
        }

        public override void OnVideoError(VideoError videoError)
        {
            _currentPlayer.Stop();
            Debug.LogError("Video failed: " + _syncedURL);
        }

        public override void OnDeserialization()
        {
            // Load new video when _videoNumber is changed
            if (Networking.IsOwner(gameObject))
                return;

            if (_videoNumber == _loadedVideoNumber)
                return;

            _currentPlayer.Stop();
            _currentPlayer.LoadURL(_syncedURL);

            SyncVideo();

            _loadedVideoNumber = _videoNumber;

            Debug.Log("Playing synced " + _syncedURL);
        }

        private void Update()
        {
            if (Networking.IsOwner(gameObject) || !_waitForSync)
            {
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
                Debug.LogFormat("Syncing Video to {0:N2}", offsetTime);
            }
        }
    }
}
