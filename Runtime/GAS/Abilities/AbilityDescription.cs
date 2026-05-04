using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
        public string Text;
        public override string GetText(GameplayAbility ability, AbilitySystemComponent source) => Text ?? string.Empty;
    }

    [Serializable]
    public class AttributeElement : DescriptionElement
    {
        public AttributeDefinition Attribute;
        public Color Color = Color.yellow;
        public string Format = "F1";

        public override string GetText(GameplayAbility ability, AbilitySystemComponent source)
        {
            float value = source != null ? source.GetAttributeValue(Attribute) : 0f;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(Color)}>{value.ToString(Format)}</color>";
        }
    }

    [Serializable]
    public class TemplateElement : DescriptionElement
    {
        [TextArea(3, 10)]
        public string Template;

        [Serializable]
        public struct TagMapping
        {
            public string Tag;
            public AttributeDefinition Attribute;
            public string Format;
        }

        public List<TagMapping> Mappings = new List<TagMapping>();

        public override string GetText(GameplayAbility ability, AbilitySystemComponent source)
        {
            if (string.IsNullOrEmpty(Template)) return string.Empty;

            string result = Template;
            foreach (var mapping in Mappings)
            {
                float value = source != null ? source.GetAttributeValue(mapping.Attribute) : 0f;
                string format = string.IsNullOrEmpty(mapping.Format) ? "F1" : mapping.Format;
                result = result.Replace($"{{{mapping.Tag}}}", value.ToString(format));
            }
            return result;
        }
    }

    [Serializable]
    public class AbilityDescription
    {
        [SerializeReference]
        public List<DescriptionElement> Elements = new List<DescriptionElement>();

        public string GetFullDescription(GameplayAbility ability, AbilitySystemComponent source)
        {
            if (Elements == null || Elements.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (var element in Elements)
            {
                sb.Append(element.GetText(ability, source));
            }
            return sb.ToString();
        }
    }
}
