
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video.Examples
{
    /// <summary>
    /// Plays a specified video URL when OnButtonPress is triggered by a button in this example. See the URLButton prefab for a use of this.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("Udon Sharp/Video/Examples/Play URL Button")]
    public class PlayURLButton : UdonSharpBehaviour
    {
        public USharpVideoPlayer targetVideoPlayer;
        public VRCUrl url = VRCUrl.Empty;

        Button button;

        void Start()
        {
            button = GetComponentInChildren<Button>();
            UpdateOwnership();

            targetVideoPlayer.RegisterCallbackReceiver(this);
        }

        public void OnButtonPress()
        {
            targetVideoPlayer.PlayVideo(url);
        }

        public void OnUSharpVideoLockChange()
        {
            UpdateOwnership();
        }

        public void OnUSharpVideoOwnershipChange()
        {
            UpdateOwnership();
        }

        void UpdateOwnership()
        {
            button.interactable = targetVideoPlayer.CanControlVideoPlayer();
        }
    }
}
