
using UnityEngine;
using System;
using static UdonSharp.Video.UI.UIStyleMarkup;

namespace UdonSharp.Video.UI
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    internal class StyleMarkupLinkAttribute : Attribute
    {
        public StyleClass Class { get; private set; }

        private StyleMarkupLinkAttribute() { }

        public StyleMarkupLinkAttribute(StyleClass styleClass)
        {
            Class = styleClass;
        }
    }

    internal class UIStyle : ScriptableObject
    {
        [StyleMarkupLink(StyleClass.Background)]
        public Color backgroundColor = Color.black;
        [StyleMarkupLink(StyleClass.FieldBackground)]
        public Color fieldBackgroundColor = Color.black;
        [StyleMarkupLink(StyleClass.ButtonBackground)]
        public Color buttonBackgroundColor = Color.black;
        [StyleMarkupLink(StyleClass.ScrollBarHandle)]
        public Color scrollBarHandleColor = Color.black;
        [StyleMarkupLink(StyleClass.ScrollBarProgress)]
        public Color scrollBarProgressColor = Color.black;
        [StyleMarkupLink(StyleClass.Icon)]
        public Color iconColor = Color.black;
        [StyleMarkupLink(StyleClass.IconDropShadow)]
        public Color iconDropShadowColor = Color.black;
        [StyleMarkupLink(StyleClass.HighlightedButton)]
        public Color highlightedButtonColor = Color.black;
        [StyleMarkupLink(StyleClass.PlaceholderText)]
        public Color placeholderTextColor = Color.black;
        [StyleMarkupLink(StyleClass.Text)]
        public Color textColor = Color.black;
        [StyleMarkupLink(StyleClass.TextDropShadow)]
        public Color textDropShadowColor = Color.black;
        [StyleMarkupLink(StyleClass.InvertedText)]
        public Color invertedTextColor = Color.black;
        [StyleMarkupLink(StyleClass.RedIcon)]
        public Color redIconColor = Color.black;
        [StyleMarkupLink(StyleClass.InvertedIcon)]
        public Color invertedIconColor = Color.black;
        [StyleMarkupLink(StyleClass.TextHighlight)]
        public Color textHighlightColor = Color.black;
        [StyleMarkupLink(StyleClass.TextCaret)]
        public Color textCaretColor = Color.white;
    }
}
