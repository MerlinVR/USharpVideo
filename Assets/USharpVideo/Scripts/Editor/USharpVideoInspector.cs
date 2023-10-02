
using UnityEditor;
using UdonSharpEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Video.Components.AVPro;
using System.Reflection;

#pragma warning disable CS0612 // Type or member is obsolete

namespace UdonSharp.Video.Internal
{
    [CustomEditor(typeof(USharpVideoPlayer))]
    internal class USharpVideoInspector : Editor
    {
        ReorderableList playlistList;

        SerializedProperty allowSeekProperty;
        SerializedProperty defaultUnlockedProperty;
        SerializedProperty allowCreatorControlProperty;

        SerializedProperty syncFrequencyProperty;
        SerializedProperty syncThresholdProperty;

        SerializedProperty defaultVolumeProperty;
        SerializedProperty audioRangeProperty;

        SerializedProperty defaultStreamMode;

        SerializedProperty playlistProperty;
        SerializedProperty loopPlaylistProperty;
        SerializedProperty shufflePlaylistProperty;
        
        private void OnEnable()
        {
            allowSeekProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.allowSeeking));
            defaultUnlockedProperty = serializedObject.FindProperty("defaultUnlocked");
            allowCreatorControlProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.allowInstanceCreatorControl));
            syncFrequencyProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.syncFrequency));
            syncThresholdProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.syncThreshold));

            defaultVolumeProperty = serializedObject.FindProperty("defaultVolume");
            audioRangeProperty = serializedObject.FindProperty("audioRange");

            defaultStreamMode = serializedObject.FindProperty("defaultStreamMode");

            playlistProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.playlist));
            loopPlaylistProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.loopPlaylist));
            shufflePlaylistProperty = serializedObject.FindProperty(nameof(USharpVideoPlayer.shufflePlaylist));

            playlistList = new ReorderableList(serializedObject, playlistProperty, true, true, true, true);
            playlistList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                Rect testFieldRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);

                EditorGUI.PropertyField(testFieldRect, playlistList.serializedProperty.GetArrayElementAtIndex(index), label: new GUIContent());
            };
            playlistList.drawHeaderCallback = (Rect rect) => { EditorGUI.LabelField(rect, new GUIContent("Default Playlist URLs", "URLs that will play in sequence when you join the world until someone puts in a video.")); };
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawConvertToUdonBehaviourButton(target) ||
                UdonSharpGUI.DrawProgramSource(target))
                return;

            UdonSharpGUI.DrawUILine();

            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(allowSeekProperty);
            EditorGUILayout.PropertyField(defaultUnlockedProperty);
            EditorGUILayout.PropertyField(allowCreatorControlProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sync", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(syncFrequencyProperty);
            EditorGUILayout.PropertyField(syncThresholdProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
            EditorGUILayout.PropertyField(defaultVolumeProperty);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.PropertyField(audioRangeProperty);

            if (EditorGUI.EndChangeCheck())
            {
                VideoPlayerManager manager = ((Component)target).GetUdonSharpComponentInChildren<VideoPlayerManager>(true);

                foreach (AudioSource source in manager.audioSources)
                {
                    if (source)
                    {
                        Undo.RecordObject(source, "Change audio properties");
                        source.maxDistance = Mathf.Max(0f, audioRangeProperty.floatValue);
                        source.volume = defaultVolumeProperty.floatValue;

                        if (PrefabUtility.IsPartOfPrefabInstance(source))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                    }
                }

                VolumeController[] volumeControllers = ((Component)target).GetUdonSharpComponentsInChildren<VolumeController>(true);

                foreach (VolumeController controller in volumeControllers)
                {
                    if (controller.slider)
                    {
                        Undo.RecordObject(controller.slider, "Change audio properties");
                        controller.slider.value = defaultVolumeProperty.floatValue;

                        if (PrefabUtility.IsPartOfPrefabInstance(controller.slider))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(controller.slider);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Playlist", EditorStyles.boldLabel);

            playlistList.DoLayoutList();
            EditorGUILayout.PropertyField(loopPlaylistProperty);
            EditorGUILayout.PropertyField(shufflePlaylistProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stream Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(defaultStreamMode);

            VRCAVProVideoPlayer avProPlayer = ((Component)target).GetComponentInChildren<VRCAVProVideoPlayer>(true);

            if (avProPlayer)
            {
                EditorGUI.BeginChangeCheck();
                bool newLowLatencyMode = EditorGUILayout.Toggle(new GUIContent("Low Latency Stream", "Whether the stream player should use low latency mode for RTSP streams"), avProPlayer.UseLowLatency);

                if (EditorGUI.EndChangeCheck())
                {
                    //FieldInfo lowLatencyField = typeof(VRCAVProVideoPlayer).GetField("useLowLatency", BindingFlags.Instance | BindingFlags.NonPublic);

                    SerializedObject avproPlayerSerializedObject = new SerializedObject(avProPlayer);
                    SerializedProperty lowLatencyField = avproPlayerSerializedObject.FindProperty("useLowLatency");

                    lowLatencyField.boolValue = newLowLatencyMode;
                    avproPlayerSerializedObject.ApplyModifiedProperties();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
