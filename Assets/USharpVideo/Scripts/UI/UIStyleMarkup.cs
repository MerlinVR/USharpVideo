using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UdonSharp.Video.UI
{
    [AddComponentMenu("Udon Sharp/Video/UI/Style Markup")]
    internal class UIStyleMarkup : MonoBehaviour
    {
        public enum StyleClass
        {
            Background,
            FieldBackground,
            ButtonBackground,
            ScrollBarHandle,
            ScrollBarProgress,
            Icon,
            IconDropShadow,
            HighlightedButton,
            PlaceholderText,
            Text,
            TextDropShadow,
            InvertedText,
            RedIcon,
            InvertedIcon,
            TextHighlight,
            TextCaret,
        }

#pragma warning disable CS0649
        public StyleClass styleClass;
        public Graphic targetGraphic;
#pragma warning restore CS0649

        private void Reset()
        {
            hideFlags = HideFlags.DontSaveInBuild;
            targetGraphic = GetComponent<Graphic>();
        }
    }
}
