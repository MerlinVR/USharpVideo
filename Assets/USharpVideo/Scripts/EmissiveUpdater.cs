
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    [AddComponentMenu("Udon Sharp/Video/Emissive Updater")]
    public class EmissiveUpdater : UdonSharpBehaviour
    {
        MeshRenderer rendererToUpdate;

        void Start()
        {
            rendererToUpdate = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
            RendererExtensions.UpdateGIMaterials(rendererToUpdate);
        }
    }
}
