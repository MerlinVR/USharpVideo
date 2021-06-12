
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video.Examples
{
    /// <summary>
    /// Very basic example script used to call Resync on a target video player.
    /// You could use SendCustomEvent on the video player itself from the click events on the button, but that's not as easy to document and explain
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Examples/Reload Button")]
    public class ReloadButton : UdonSharpBehaviour
    {
        public USharpVideoPlayer targetVideoPlayer;

        public void OnResyncButtonPress()
        {
            if (targetVideoPlayer)
                targetVideoPlayer.Reload();
            else
                Debug.LogError($"Resync button {this} does not have a valid target video player!", this);
        }
    }
}
