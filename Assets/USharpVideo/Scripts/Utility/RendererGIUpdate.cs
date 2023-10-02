
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Utilities/Renderer GI Update")]
    public class RendererGIUpdate : UdonSharpBehaviour
    {
        private Renderer targetRenderer;

        void Start()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            RendererExtensions.UpdateGIMaterials(targetRenderer);
        }
    }
}
