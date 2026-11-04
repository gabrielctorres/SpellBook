using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using UnityEngine;

namespace SpellBook.GAS.Description
{
    /// <summary>
    /// Parte da descrição de uma habilidade. Acrescenta linhas a <see cref="DescriptionContext.Lines"/>, sem limpar e sem
    /// marcação; o texto sai pelo <see cref="DescriptionWriter"/>. Para um elemento próprio, derive e implemente
    /// <see cref="Describe"/>.
    /// </summary>
    [Serializable]
    public abstract class DescriptionElement
    {
        /// <summary>Acrescenta as linhas deste elemento a <c>context.Lines</c>.</summary>
        public abstract void Describe(DescriptionContext context);

        /// <summary>
        /// Acusa o que este elemento não consegue descrever em <paramref name="owner"/>, no campo
        /// <paramref name="propertyPath"/> (o do elemento). O padrão não acusa nada.
        /// </summary>
        public virtual void Validate(ValidationResult result, string propertyPath, GameplayAbility owner)
        {
        }
    }

    /// <summary>Descrição de uma habilidade: os elementos, na ordem em que aparecem.</summary>
    [Serializable]
    public class AbilityDescription
    {
        [SerializeReference]
        [TypePicker]
        public List<DescriptionElement> Elements = new List<DescriptionElement>();

        /// <summary>Acrescenta as linhas de cada elemento, na ordem, pulando entradas nulas.</summary>
        public void Describe(DescriptionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (Elements == null) return;

            for (int i = 0; i < Elements.Count; i++)
            {
                Elements[i]?.Describe(context);
            }
        }

        /// <summary>
        /// Acusa elemento nulo (erro), repassa a cada elemento o caminho dele e avisa quando a mesma chave de texto aparece
        /// com textos padrão diferentes. <paramref name="propertyPath"/> é o do campo da descrição na habilidade.
        /// </summary>
        public void Validate(ValidationResult result, string propertyPath, GameplayAbility owner)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (Elements == null) return;

            for (int i = 0; i < Elements.Count; i++)
            {
                string path = $"{propertyPath}.{nameof(Elements)}.Array.data[{i}]";
                DescriptionElement element = Elements[i];
                if (element == null)
                {
                    result.AddError("Elemento de descrição vazio: escolha o tipo ou remova o elemento.", path);
                    continue;
                }

                element.Validate(result, path, owner);
                ValidateKey(result, i, path);
            }
        }

        // Compara a chave do elemento i com as dos anteriores; avisa uma vez, no elemento que repete.
        private void ValidateKey(ValidationResult result, int index, string path)
        {
            if (!TryGetText(Elements[index], out LocalizedText text, out string field) || string.IsNullOrEmpty(text.Key)) return;

            for (int j = 0; j < index; j++)
            {
                if (!TryGetText(Elements[j], out LocalizedText other, out _)) continue;
                if (!string.Equals(other.Key, text.Key, StringComparison.Ordinal)) continue;
                if (string.Equals(other.Text, text.Text, StringComparison.Ordinal)) continue;

                result.AddWarning(
                    $"A chave \"{text.Key}\" já aparece no elemento {j} com outro texto padrão: sem provedor, os dois textos diferem.",
                    path + "." + field);
                return;
            }
        }

        private static bool TryGetText(DescriptionElement element, out LocalizedText text, out string field)
        {
            switch (element)
            {
                case TextElement textElement:
                    text = textElement.Text;
                    field = TextElement.TextField;
                    return true;
                case TemplateElement template:
                    text = template.Template;
                    field = TemplateElement.TemplateField;
                    return true;
                default:
                    text = default;
                    field = null;
                    return false;
            }
        }
    }
}
