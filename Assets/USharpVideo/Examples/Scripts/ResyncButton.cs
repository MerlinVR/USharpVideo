
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video.Examples
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Examples/Resync Button")]
    public class ResyncButton : UdonSharpBehaviour
    {
        public USharpVideoPlayer videoPlayer;

        public void OnResyncPressed()
        {
            videoPlayer.ForceSyncVideo();
        }
    }
}
