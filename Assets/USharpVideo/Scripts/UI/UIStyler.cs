
using TMPro;
using UnityEngine;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using VRC.SDK3.Components;

#if UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

#pragma warning disable CS0612 // Type or member is obsolete

namespace UdonSharp.Video.UI
{
    [AddComponentMenu("Udon Sharp/Video/UI/Styler")]
    internal class UIStyler : MonoBehaviour
    {
#pragma warning disable CS0649
        public UIStyle uiStyle;
#pragma warning restore CS0649

        private void Reset()
        {
            hideFlags = HideFlags.DontSaveInBuild;
        }

        private static Dictionary<UIStyleMarkup.StyleClass, FieldInfo> GetStyleFieldMap()
        {
            Dictionary<UIStyleMarkup.StyleClass, FieldInfo> fieldLookup = new Dictionary<UIStyleMarkup.StyleClass, FieldInfo>();

            foreach (FieldInfo field in typeof(UIStyle).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(Color))
                {
                    StyleMarkupLinkAttribute markupAttr = field.GetCustomAttribute<StyleMarkupLinkAttribute>();

                    if (markupAttr != null)
                    {
                        fieldLookup.Add(markupAttr.Class, field);
                    }
                }
            }

            return fieldLookup;
        }

#if UNITY_EDITOR
    private Color GetColor(FieldInfo field)
        {
            return (Color)field.GetValue(uiStyle);
        }

        public void ApplyStyle()
        {
            if (uiStyle == null)
                return;

            var lookup = GetStyleFieldMap();

            UIStyleMarkup[] markups = GetComponentsInChildren<UIStyleMarkup>(true);

            foreach (UIStyleMarkup markup in markups)
            {
                Color graphicColor = GetColor(lookup[markup.styleClass]);

                if (markup.styleClass == UIStyleMarkup.StyleClass.TextHighlight)
                {
                    InputField input = markup.GetComponent<InputField>();
                    TMP_InputField inputTmp = markup.GetComponent<TMP_InputField>();
                    VRCUrlInputField vrcInput = markup.GetComponent<VRCUrlInputField>();

                    if (input != null)
                    {
                        Undo.RecordObject(input, "Apply UI Style");
                        input.selectionColor = graphicColor;
                        RecordObject(input);
                    }
                    else if (inputTmp != null)
                    {
                        Undo.RecordObject(inputTmp, "Apply UI Style");
                        inputTmp.selectionColor = graphicColor;
                        RecordObject(inputTmp);
                    }
                    else if (vrcInput != null)
                    {
                        Undo.RecordObject(vrcInput, "Apply UI Style");
                        vrcInput.selectionColor = graphicColor;
                        RecordObject(vrcInput);
                    }
                }
                else if (markup.styleClass == UIStyleMarkup.StyleClass.TextCaret)
                {
                    InputField input = markup.GetComponent<InputField>();
                    TMP_InputField inputTmp = markup.GetComponent<TMP_InputField>();
                    VRCUrlInputField vrcInput = markup.GetComponent<VRCUrlInputField>();

                    if (input != null)
                    {
                        Undo.RecordObject(input, "Apply UI Style");
                        input.caretColor = graphicColor;
                        RecordObject(input);
                    }
                    else if (inputTmp != null)
                    {
                        Undo.RecordObject(inputTmp, "Apply UI Style");
                        inputTmp.caretColor = graphicColor;
                        RecordObject(inputTmp);
                    }
                    else if (vrcInput != null)
                    {
                        Undo.RecordObject(vrcInput, "Apply UI Style");
                        vrcInput.caretColor = graphicColor;
                        RecordObject(vrcInput);
                    }
                }
                else if (markup.targetGraphic != null)
                {
                    Undo.RecordObject(markup.targetGraphic, "Apply UI Style");
                    markup.targetGraphic.color = graphicColor;
                    RecordObject(markup.targetGraphic);
                }
            }

            foreach (VideoControlHandler controlHandler in this.GetUdonSharpComponentsInChildren<VideoControlHandler>(true))
            {
                Undo.RecordObject(controlHandler, "Apply UI Style");

                controlHandler.whiteGraphicColor = GetColor(lookup[UIStyleMarkup.StyleClass.Icon]);
                controlHandler.redGraphicColor = GetColor(lookup[UIStyleMarkup.StyleClass.RedIcon]);
                controlHandler.buttonBackgroundColor = GetColor(lookup[UIStyleMarkup.StyleClass.ButtonBackground]);
                controlHandler.buttonActivatedColor = GetColor(lookup[UIStyleMarkup.StyleClass.HighlightedButton]);
                controlHandler.iconInvertedColor = GetColor(lookup[UIStyleMarkup.StyleClass.InvertedIcon]);

                controlHandler.ApplyProxyModifications();

                if (PrefabUtility.IsPartOfPrefabInstance(controlHandler.gameObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(controlHandler));
            }
        }

        private static void RecordObject(Object comp)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(comp))
                PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UIStyler))]
    internal class UIStylerEditor : Editor
    {
        private SerializedProperty colorStyleProperty;

        private void OnEnable()
        {
            colorStyleProperty = serializedObject.FindProperty(nameof(UIStyler.uiStyle));
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(colorStyleProperty);

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
                (target as UIStyler).ApplyStyle();

            if (colorStyleProperty.objectReferenceValue is UIStyle style)
            {
                //EditorGUILayout.Space();

                //if (GUILayout.Button("Apply Style"))
                //    (target as UIStyler).ApplyStyle();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(style.name), EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();

                SerializedObject styleObj = new SerializedObject(style);

                foreach (FieldInfo field in typeof(UIStyle).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    SerializedProperty property = styleObj.FindProperty(field.Name);

                    if (property != null)
                        EditorGUILayout.PropertyField(property);
                }

                styleObj.ApplyModifiedProperties();

                if (EditorGUI.EndChangeCheck())
                    (target as UIStyler).ApplyStyle();
            }
            else
            {
                EditorGUILayout.Space();

                if (GUILayout.Button("Create New Style"))
                {
                    string saveLocation = EditorUtility.SaveFilePanelInProject("Style save location", "Style", "asset", "Choose a save location for the new style");

                    if (!string.IsNullOrEmpty(saveLocation))
                    {
                        var newStyle = ScriptableObject.CreateInstance<UIStyle>();

                        newStyle.name = Path.GetFileNameWithoutExtension(saveLocation); // I'm not sure if the name gets updated when someone changes the name manually so this may need to be revisited

                        AssetDatabase.CreateAsset(newStyle, saveLocation);
                        AssetDatabase.SaveAssets();

                        serializedObject.FindProperty(nameof(UIStyler.uiStyle)).objectReferenceValue = newStyle;
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }
        }
    }
#endif
}
