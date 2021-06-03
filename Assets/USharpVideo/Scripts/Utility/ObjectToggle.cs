
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Video
{
    public class ObjectToggle : UdonSharpBehaviour
    {
#pragma warning disable CS0649
        [SerializeField]
        GameObject[] toggleObjects;
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
