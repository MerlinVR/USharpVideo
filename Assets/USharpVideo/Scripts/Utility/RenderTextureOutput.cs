
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System.IO;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UdonSharpEditor;
#endif

namespace UdonSharp.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Utilities/Render Texture Output")]
    public class RenderTextureOutput : UdonSharpBehaviour
    {
#pragma warning disable CS0649
        [SerializeField]
        USharpVideoPlayer sourceVideoPlayer;
        VideoPlayerManager videoPlayerManager;
#pragma warning restore CS0649

        public CustomRenderTexture outputTexture;

        Material outputMat;

        private void Start()
        {
            outputMat = outputTexture.material;
            videoPlayerManager = sourceVideoPlayer.GetComponentInChildren<VideoPlayerManager>(true);
        }

        Texture lastTex;

        private void LateUpdate()
        {
            Texture videoPlayerTex = videoPlayerManager.GetVideoTexture();

            if (lastTex != videoPlayerManager)
            {
                outputMat.SetTexture("_SourceTexture", videoPlayerTex);
                outputMat.SetInt("_IsAVPro", System.Convert.ToInt32(sourceVideoPlayer.IsUsingAVProPlayer()));

                lastTex = videoPlayerTex;
            }
        }
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    [CustomEditor(typeof(RenderTextureOutput))]
    internal class RenderTextureOutputInspector : Editor
    {
        internal class RenderTextureCreator : EditorWindow
        {
            public RenderTextureOutput targetOutput;

            Vector2Int resolution = new Vector2Int(1920, 1080);

            private void OnGUI()
            {
                targetOutput = (RenderTextureOutput)EditorGUILayout.ObjectField("Target component", targetOutput, typeof(RenderTextureOutput), true);
                resolution = EditorGUILayout.Vector2IntField("Resolution", resolution);

                if (GUILayout.Button("Create Texture"))
                {
                    CreateCRT();
                }
            }

            void CreateCRT()
            {
                string filePath = EditorUtility.SaveFilePanelInProject("Texture save", "VideoOutputTexture", "asset", "Choose a name for the texture file");

                if (string.IsNullOrEmpty(filePath))
                    return;

                CustomRenderTexture newCRT = new CustomRenderTexture(resolution.x, resolution.y, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                newCRT.autoGenerateMips = false;
                newCRT.initializationMode = CustomRenderTextureUpdateMode.OnLoad;
                newCRT.initializationColor = Color.black;
                newCRT.initializationSource = CustomRenderTextureInitializationSource.TextureAndColor;

                newCRT.updateMode = CustomRenderTextureUpdateMode.Realtime;

                Material updateMat = new Material(Shader.Find("Merlin/World/Render Texture Processor"));
                updateMat.name = $"{Path.GetFileNameWithoutExtension(filePath)}_Update";
                updateMat.SetFloat("_TargetAspectRatio", resolution.x / (float)resolution.y);
                
                AssetDatabase.CreateAsset(newCRT, filePath);
                AssetDatabase.AddObjectToAsset(updateMat, newCRT);

                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(updateMat));

                newCRT.material = updateMat;
                AssetDatabase.SaveAssets();

                targetOutput.outputTexture = newCRT;

                targetOutput.ApplyProxyModifications();
            }
        }

        SerializedProperty sourceVideoPlayerProperty;
        SerializedProperty outputTextureProperty;

        private void OnEnable()
        {
            sourceVideoPlayerProperty = serializedObject.FindProperty("sourceVideoPlayer");
            outputTextureProperty = serializedObject.FindProperty("outputTexture");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            EditorGUILayout.PropertyField(sourceVideoPlayerProperty);

            if (sourceVideoPlayerProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox("A source video player must be specified", MessageType.Error);

            EditorGUILayout.PropertyField(outputTextureProperty);

            serializedObject.ApplyModifiedProperties();

            RenderTextureOutput output = (RenderTextureOutput)target;

            if (output.outputTexture == null)
            {
                EditorGUILayout.Space();

                if (GUILayout.Button("Setup Output Texture", GUILayout.Height(30f)))
                {
                    RenderTextureCreator window = EditorWindow.GetWindow<RenderTextureCreator>(false, "Render Texture Creator");
                    window.targetOutput = output;
                    window.maxSize = new Vector2(450f, 100f);
                    window.minSize = new Vector2(450f, 100f);
                }
            }
        }
    }
#endif
}
