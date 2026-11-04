using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
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
    }
}
