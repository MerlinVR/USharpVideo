
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class RendererGIUpdate : UdonSharpBehaviour
    {
        Renderer targetRenderer;

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
