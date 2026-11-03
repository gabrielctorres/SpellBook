using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Targeting
{
    /// <summary>
    /// Escolhe os alvos de uma ação. O executor chama <see cref="Select"/> quando a ação começa e entrega o resultado em
    /// <see cref="ActionContext.Targets"/>. O seletor é dado compartilhado: não guarde estado de execução nele.
    /// </summary>
    [Serializable]
    public abstract class TargetSelector
    {
        /// <summary>
        /// Acrescenta os alvos a <paramref name="results"/>, que chega vazia. Use só listas e buffers reutilizados:
        /// o executor chama este método a cada ação que começa.
        /// </summary>
        public abstract void Select(ActionContext context, List<AbilitySystemComponent> results);

        /// <summary>Se o seletor lê os alvos do evento. Serve ao Inspector e ao validador.</summary>
        public virtual bool NeedsEventTargets => false;
    }

    /// <summary>Escolhe quem lançou (<see cref="ActionContext.Instigator"/>).</summary>
    [Serializable, TypeCategory("Alvo", Summary = "Escolhe quem lançou a habilidade ou aplicou o efeito.", Line = "Quem lançou")]
    public sealed class CasterSelector : TargetSelector
    {
        public override void Select(ActionContext context, List<AbilitySystemComponent> results)
        {
            if (context.Instigator != null) results.Add(context.Instigator);
        }
    }

    /// <summary>Escolhe os alvos do evento (<see cref="ActionContext.EventTargets"/>), sem os destruídos.</summary>
    [Serializable, TypeCategory("Alvo", Summary = "Escolhe os alvos do evento que disparou a lista.", Line = "Alvos do evento")]
    public sealed class EventTargetsSelector : TargetSelector
    {
        public override bool NeedsEventTargets => true;

        public override void Select(ActionContext context, List<AbilitySystemComponent> results)
        {
            List<AbilitySystemComponent> eventTargets = context.EventTargets;
            for (int i = 0; i < eventTargets.Count; i++)
            {
                if (eventTargets[i] != null) results.Add(eventTargets[i]);
            }
        }
    }

    /// <summary>Não escolhe unidade: a ação usa o ponto do evento (<see cref="ActionContext.Point"/>).</summary>
    [Serializable, TypeCategory("Alvo", Summary = "Não escolhe unidade; a ação usa o ponto do evento.", Line = "Ponto")]
    public sealed class PointSelector : TargetSelector
    {
        public override void Select(ActionContext context, List<AbilitySystemComponent> results) { }
    }
}
