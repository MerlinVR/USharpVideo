
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video.Internal
{
    [AddComponentMenu("Udon Sharp/Video/Internal/Object Toggle")]
    public class ObjectToggle : UdonSharpBehaviour
    {
#pragma warning disable CS0649
        [SerializeField]
        private GameObject[] toggleObjects;
#pragma warning restore CS0649

        public void OnToggle()
        {
            foreach (GameObject toggleObject in toggleObjects)
            {
                toggleObject.SetActive(!toggleObject.activeSelf);
            }
        }
    }
}
