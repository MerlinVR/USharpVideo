
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UdonSharpEditor;
#endif

namespace UdonSharp.Video
{
    /// <summary>
    /// Forwards events sent by Udon video player components to the main video player controller and further abstracts the video players between AVPro and Unity players
    /// This exists so that we can put the Udon video player components on a different object from the main video player
    /// Prior to using this, people would get confused and change settings on the Udon video player components which would break things
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Internal/Video Player Manager")]
    public class VideoPlayerManager : UdonSharpBehaviour
    {
        public USharpVideoPlayer receiver;

        public VRCUnityVideoPlayer unityVideoPlayer;
        public VRCAVProVideoPlayer avProPlayer;
        public Renderer unityTextureRenderer;
        public Renderer avProTextureRenderer;
        public AudioSource[] audioSources;

        BaseVRCVideoPlayer _currentPlayer;
        MaterialPropertyBlock _fetchBlock;
        Material avproFetchMaterial;

        bool _initialized = false;

        public void Start()
        {
            if (_initialized)
                return;

            _currentPlayer = unityVideoPlayer;

            Material m = unityTextureRenderer.material;
            m = avProTextureRenderer.material;
            _fetchBlock = new MaterialPropertyBlock();
            avproFetchMaterial = avProTextureRenderer.material;

            _initialized = true;
        }

        public override void OnVideoEnd()
        {
            receiver.OnVideoEnd();
        }

        public override void OnVideoError(VRC.SDK3.Components.Video.VideoError videoError)
        {
            receiver._OnVideoErrorCallback(videoError);
        }

        public override void OnVideoLoop()
        {
            receiver.OnVideoLoop();
        }

        public override void OnVideoPause()
        {
            receiver.OnVideoPause();
        }

        public override void OnVideoPlay()
        {
            receiver.OnVideoPlay();
        }

        public override void OnVideoReady()
        {
            receiver.OnVideoReady();
        }

        public override void OnVideoStart()
        {
            receiver.OnVideoStart();
        }

        public void Play() => _currentPlayer.Play();
        public void Pause() => _currentPlayer.Pause();
        public void Stop() => _currentPlayer.Stop();
        public float GetTime() => _currentPlayer.GetTime();
        public float GetDuration() => _currentPlayer.GetDuration();
        public bool IsPlaying() => _currentPlayer.IsPlaying;
        public void LoadURL(VRCUrl url) => _currentPlayer.LoadURL(url);
        public void SetTime(float time) => _currentPlayer.SetTime(time);

        public void SetLooping(bool loop)
        {
            unityVideoPlayer.Loop = loop;
            avProPlayer.Loop = loop;
        }

        public void SetToStreamPlayerMode()
        {
            if (_currentPlayer == avProPlayer)
                return;

            _currentPlayer.Stop();
            _currentPlayer = avProPlayer;
        }

        public void SetToVideoPlayerMode()
        {
            if (_currentPlayer == unityVideoPlayer)
                return;

            _currentPlayer.Stop();
            _currentPlayer = unityVideoPlayer;
        }

        public Texture GetVideoTexture()
        {
            if (_currentPlayer == unityVideoPlayer)
            {
                unityTextureRenderer.GetPropertyBlock(_fetchBlock);

                return _fetchBlock.GetTexture("_MainTex");
            }
            else
            {
                return avproFetchMaterial.GetTexture("_MainTex");
            }
        }

        float _currentVolume = 1f;
        bool _currentlyMuted = false;

        public float GetVolume() => _currentVolume;
        public bool IsMuted() => _currentlyMuted;

        public void SetVolume(float volume)
        {
            if (!_currentlyMuted)
            {
                // https://www.dr-lex.be/info-stuff/volumecontrols.html#ideal thanks TCL for help with finding and understanding this
                // Using the 50dB dynamic range constants
                float adjustedVolume = Mathf.Clamp01(3.1623e-3f * Mathf.Exp(volume * 5.757f) - 3.1623e-3f);

                foreach (AudioSource audioSource in audioSources)
                    audioSource.volume = adjustedVolume;
            }

            _currentVolume = volume;
        }

        public void SetMuted(bool muted)
        {
            _currentlyMuted = muted;

            if (muted)
            {
                foreach (AudioSource audioSource in audioSources)
                    audioSource.volume = 0f;
            }
            else
            {
                SetVolume(_currentVolume);
            }
        }
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    [CustomEditor(typeof(VideoPlayerManager))]
    class VideoPlayerManagerInspector : Editor
    {
        SerializedProperty receiverProperty;
        SerializedProperty unityVideoProperty;
        SerializedProperty avProVideoProperty;
        SerializedProperty unityRendererProperty;
        SerializedProperty avProRendererProperty;
        SerializedProperty audioSourcesProperty;

        private void OnEnable()
        {
            receiverProperty = serializedObject.FindProperty(nameof(VideoPlayerManager.receiver));
            unityVideoProperty = serializedObject.FindProperty(nameof(VideoPlayerManager.unityVideoPlayer));
            avProVideoProperty = serializedObject.FindProperty(nameof(VideoPlayerManager.avProPlayer));
            unityRendererProperty = serializedObject.FindProperty(nameof(VideoPlayerManager.unityTextureRenderer));
            avProRendererProperty = serializedObject.FindProperty(nameof(VideoPlayerManager.avProTextureRenderer));
            audioSourcesProperty = serializedObject.FindProperty(nameof(VideoPlayerManager.audioSources));
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawConvertToUdonBehaviourButton(target)) return;
            if (UdonSharpGUI.DrawProgramSource(target, false)) return;

            EditorGUILayout.HelpBox("Do not modify the video players on this game object, all modifications must be done on the USharpVideoPlayer. If you change the settings on these, you will break things.", MessageType.Warning);
            EditorGUILayout.PropertyField(receiverProperty);
            EditorGUILayout.PropertyField(unityVideoProperty);
            EditorGUILayout.PropertyField(avProVideoProperty);
            EditorGUILayout.PropertyField(unityRendererProperty);
            EditorGUILayout.PropertyField(avProRendererProperty);
            EditorGUILayout.PropertyField(audioSourcesProperty, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
