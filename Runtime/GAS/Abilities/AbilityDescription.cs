using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Sirenix.OdinInspector;
using SpellBook.GAS.Core;
using SpellBook.GAS.Attributes;

namespace SpellBook.GAS.Abilities
{
    [Serializable]
    public abstract class DescriptionElement
    {
        public abstract string GetText(GameplayAbility ability, AbilitySystemComponent source);
    }

    [Serializable]
    public class TextElement : DescriptionElement
    {
        [HideLabel]
        public string Text;
        public override string GetText(GameplayAbility ability, AbilitySystemComponent source) => Text ?? string.Empty;
    }

    [Serializable]
    public class AttributeElement : DescriptionElement
    {
        public AttributeDefinition Attribute;
        public Color Color = Color.yellow;
        public string Format = "F1";
        public string Placeholder = "---";

        public override string GetText(GameplayAbility ability, AbilitySystemComponent source)
        {
            if (source == null) return GetColored(Placeholder);
            return GetColored(source.GetAttributeValue(Attribute).ToString(Format));
        }

        private string GetColored(string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(Color)}>{text}</color>";
    }

    [Serializable]
    public class TemplateElement : DescriptionElement
    {
        [TextArea(2, 5)]
        public string Template;

        [Serializable]
        public struct TagMapping
        {
            [HorizontalGroup("TagData")] public string Tag;
            
            [HorizontalGroup("TagData")] 
            public AttributeDefinition Attribute;
            
            [HorizontalGroup("TagData")] public string TextReplacement;
            
            [FoldoutGroup("Format")] public string Format;
            [FoldoutGroup("Format")] public string Placeholder;
            [FoldoutGroup("Format")] public bool UseColor;
            [FoldoutGroup("Format"), ShowIf("UseColor")] public Color Color;
        }

        [TableList]
        public List<TagMapping> Mappings = new List<TagMapping>();

        public override string GetText(GameplayAbility ability, AbilitySystemComponent source)
        {
            if (string.IsNullOrEmpty(Template)) return string.Empty;

            string result = Template;
            foreach (var mapping in Mappings)
            {
                string valueStr;
                if (mapping.Attribute != null)
                {
                    valueStr = source != null ? source.GetAttributeValue(mapping.Attribute).ToString(string.IsNullOrEmpty(mapping.Format) ? "F1" : mapping.Format) 
                                            : (string.IsNullOrEmpty(mapping.Placeholder) ? "---" : mapping.Placeholder);
                }
                else
                {
                    valueStr = string.IsNullOrEmpty(mapping.TextReplacement) ? mapping.Tag : mapping.TextReplacement;
                }

                if (mapping.UseColor)
                    valueStr = $"<color=#{ColorUtility.ToHtmlStringRGB(mapping.Color)}>{valueStr}</color>";
                
                result = result.Replace($"{{{mapping.Tag}}}", valueStr);
            }
            return result;
        }
    }

    [Serializable]
    public class AbilityDescription
    {
        [SerializeReference, ListDrawerSettings(ShowIndexLabels = true, AddCopiesLastElement = true)]
        public List<DescriptionElement> Elements = new List<DescriptionElement>();

        public string GetFullDescription(GameplayAbility ability, AbilitySystemComponent source)
        {
            if (Elements == null) return string.Empty;
            StringBuilder sb = new StringBuilder();
            foreach (var element in Elements) sb.Append(element.GetText(ability, source));
            return sb.ToString();
        }
    }
}
