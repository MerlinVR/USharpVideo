#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UdonSharp.Video.Internal
{
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    public class SceneLightmapFixer : MonoBehaviour, ISerializationCallbackReceiver
    {
        [System.Serializable]
        struct RendererLightmapData
        {
            public Renderer renderer;
            public int lightmapIndex;
            public Vector4 lightmapScaleOffset;
            //public int realtimeLightmapIndex;
            //public Vector4 realtimeLightmapScaleOffset;
        }

        [System.Serializable]
        struct LightmapStorageData
        {
            public Texture2D lightmap;
            public Texture2D lightmapDir;
            public Texture2D shadowmask;
        }

        [SerializeField]
        RendererLightmapData[] rendererLightmapData;

        [SerializeField]
        LightmapStorageData[] lightmaps;

        [SerializeField]
        bool isSaved = false;

        private void Awake()
        {
            ApplyLightmapData();
        }

        public void ApplyLightmapData()
        {
            if (!isSaved)
                return;

            LightmapData[] oldLightmaps = LightmapSettings.lightmaps;

            if (oldLightmaps == null)
            {
                oldLightmaps = new LightmapData[0];
            }

            Dictionary<int, int> oldLightmapIdxToNewIdx = new Dictionary<int, int>();
            Dictionary<int, int> newLightmapIdxToOldIdx = new Dictionary<int, int>();

            for (int i = 0; i < oldLightmaps.Length; ++i)
            {
                LightmapData oldLightmap = oldLightmaps[i];

                for (int j = 0; j < lightmaps.Length; ++j)
                {
                    LightmapStorageData newLightmap = lightmaps[j];

                    if (oldLightmap.lightmapColor == newLightmap.lightmap &&
                        oldLightmap.lightmapDir == newLightmap.lightmapDir &&
                        oldLightmap.shadowMask == newLightmap.shadowmask)
                    {
                        oldLightmapIdxToNewIdx.Add(j, i);
                        newLightmapIdxToOldIdx.Add(i, j);
                        break;
                    }
                }
            }

            int newLightmapIdx = 0;

            for (int i = 0; i < lightmaps.Length; ++i)
            {
                if (!oldLightmapIdxToNewIdx.ContainsKey(i))
                {
                    newLightmapIdxToOldIdx.Add(newLightmapIdx + oldLightmaps.Length, i);
                    oldLightmapIdxToNewIdx.Add(i, oldLightmaps.Length + newLightmapIdx++);
                }
            }

            LightmapData[] newLightmapData = new LightmapData[oldLightmaps.Length + newLightmapIdx];

            for (int i = 0; i < oldLightmaps.Length; ++i)
            {
                newLightmapData[i] = new LightmapData() { lightmapColor = oldLightmaps[i].lightmapColor, lightmapDir = oldLightmaps[i].lightmapDir, shadowMask = oldLightmaps[i].shadowMask };
            }

            for (int i = 0; i < newLightmapIdx; ++i)
            {
                LightmapStorageData newData = lightmaps[newLightmapIdxToOldIdx[i + oldLightmaps.Length]];
                newLightmapData[i + oldLightmaps.Length] = new LightmapData() { lightmapColor = newData.lightmap, lightmapDir = newData.lightmapDir, shadowMask = newData.shadowmask };
            }

            LightmapSettings.lightmaps = newLightmapData;

            foreach (RendererLightmapData rendererData in rendererLightmapData)
            {
                if (rendererData.renderer != null)
                {
                    rendererData.renderer.lightmapIndex = rendererData.lightmapIndex == -1 ? -1 : oldLightmapIdxToNewIdx[rendererData.lightmapIndex];
                    rendererData.renderer.lightmapScaleOffset = rendererData.lightmapScaleOffset;
                    //rendererData.renderer.realtimeLightmapIndex = rendererData.realtimeLightmapIndex;
                    //rendererData.renderer.realtimeLightmapScaleOffset = rendererData.realtimeLightmapScaleOffset;
                }
            }
        }

        public void BuildLightmapStorage()
        {
            LightmapData[] lightmaps = LightmapSettings.lightmaps;

            LightmapStorageData[] storedLightmaps = new LightmapStorageData[lightmaps.Length];

            for (int i = 0; i < lightmaps.Length; ++i)
            {
                LightmapData currentLightmap = lightmaps[i];
                storedLightmaps[i] = new LightmapStorageData() { lightmap = currentLightmap.lightmapColor, lightmapDir = currentLightmap.lightmapDir, shadowmask = currentLightmap.shadowMask };
            }

            this.lightmaps = storedLightmaps;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            RendererLightmapData[] rendererData = new RendererLightmapData[renderers.Length];

            for (int i = 0; i < renderers.Length; ++i)
            {
                rendererData[i] = new RendererLightmapData() {
                    renderer = renderers[i],
                    lightmapIndex = renderers[i].lightmapIndex,
                    lightmapScaleOffset = renderers[i].lightmapScaleOffset,
                    //realtimeLightmapIndex = renderers[i].realtimeLightmapIndex,
                    //realtimeLightmapScaleOffset = renderers[i].realtimeLightmapScaleOffset
                };
            }

            this.rendererLightmapData = rendererData;

            isSaved = true;
        }

        // Detect if people have rebaked their lighting and clear the data if they have.
        public void OnBeforeSerialize()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length != rendererLightmapData.Length)
            {
                isSaved = false;
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                return;
            }

            for (int i = 0; i < renderers.Length; ++i)
            {
                Renderer currentRenderer = renderers[i];
                RendererLightmapData currentData = rendererLightmapData[i];

                if (currentRenderer != currentData.renderer)
                {
                    isSaved = false;
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    return;
                }

                if (currentRenderer.lightmapIndex != currentData.lightmapIndex ||
                    currentRenderer.lightmapScaleOffset != currentData.lightmapScaleOffset
                    //currentRenderer.realtimeLightmapIndex != currentData.realtimeLightmapIndex ||
                    //currentRenderer.realtimeLightmapScaleOffset != currentRenderer.realtimeLightmapScaleOffset
                    )
                {
                    isSaved = false;
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    return;
                }
            }
        }

        public void OnAfterDeserialize()
        {
        }
    }

    [CustomEditor(typeof(SceneLightmapFixer))]
    public class SceneLightmapFixerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Build lightmap storage"))
            {
                Undo.RecordObject(target, "Build lightmap data");
                ((SceneLightmapFixer)target).BuildLightmapStorage();
            }
        }
    }
}

#endif
