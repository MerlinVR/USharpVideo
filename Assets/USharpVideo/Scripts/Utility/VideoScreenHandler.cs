
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;

namespace UdonSharp.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Utilities/Video Screen Handler")]
    public class VideoScreenHandler : UdonSharpBehaviour
    {
        [SerializeField, NotNull, Tooltip("The video player that this pulls from")]
        private USharpVideoPlayer sourceVideoPlayer;

        [Header("Renderer")]
        [Tooltip("Name of parameter the shader on this renderer uses for the video texture")]
        public string texParam = "_EmissionMap";

        [Tooltip("Name of the parameter the shader on this renderer uses to determine if it should preform color and UV correction on the render texture")]
        public string avProToggleParam = "_IsAVProInput";

        [Tooltip("Sets render textures on the renderer's shared material which allows multiple screens to share the same texture and batch")]
        public bool useSharedMaterial = false;

        [Tooltip("The index of the renderer to set the video texture on")]
        public int rendererIndex = 0;

        [Tooltip("Texture that will be shown on the screen when the video player is not playing a video")]
        public Texture standbyTexture;

        private Renderer targetRenderer;
        private Texture lastRenderTexture;

        private void Start()
        {
            targetRenderer = GetComponent<Renderer>();

            OnEnable();
        }

        private void OnEnable()
        {
            SetSourceVideoPlayer(sourceVideoPlayer);
        }

        private void OnDisable()
        {
            if (sourceVideoPlayer)
                sourceVideoPlayer.UnregisterScreenHandler(this);
        }

        [PublicAPI]
        public Texture GetVideoTexture()
        {
            return lastRenderTexture;
        }

        public void UpdateVideoTexture(Texture renderTexture, bool isAVPro)
        {
            if (renderTexture == lastRenderTexture)
                return;

            if (targetRenderer)
            {
                Material rendererMat;

                // Sadly we can't use property blocks and respect the renderer index at the same time when shared materials are disabled.
                // This is because Unity is bad. Specifically when you set a property block with a specific material index, realtime GI updates do not detect the binding.
                if (useSharedMaterial)
                    rendererMat = targetRenderer.sharedMaterials[rendererIndex];
                else
                    rendererMat = targetRenderer.materials[rendererIndex];

                if (renderTexture != null)
                {
                    rendererMat.SetTexture(texParam, renderTexture);

                    if (!string.IsNullOrEmpty(avProToggleParam))
                        rendererMat.SetInt(avProToggleParam, isAVPro ? 1 : 0);
                }
                else
                {
                    rendererMat.SetTexture(texParam, standbyTexture);
                    rendererMat.SetInt(avProToggleParam, 0);
                }
            }

            lastRenderTexture = renderTexture;

            //if (renderTexture == null)
            //    Debug.Log("Null texture set");
            //else
            //    Debug.Log("Set tex to " + renderTexture);
        }

        public void SetToStandby()
        {
            Material rendererMat;

            if (useSharedMaterial)
                rendererMat = targetRenderer.sharedMaterials[rendererIndex];
            else
                rendererMat = targetRenderer.materials[rendererIndex];

            rendererMat.SetTexture(texParam, standbyTexture);

            if (!string.IsNullOrEmpty(avProToggleParam))
                rendererMat.SetInt(avProToggleParam, 0);
        }

        [PublicAPI]
        public void SetSourceVideoPlayer(USharpVideoPlayer sourcePlayer)
        {
            if (sourcePlayer)
            {
                sourceVideoPlayer.UnregisterScreenHandler(this);
                sourceVideoPlayer = sourcePlayer;
                sourceVideoPlayer.RegisterScreenHandler(this);
            }
        }
    }
}
