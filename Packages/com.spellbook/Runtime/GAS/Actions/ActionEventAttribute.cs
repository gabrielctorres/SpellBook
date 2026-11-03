using System;

namespace SpellBook.GAS.Actions
{
    /// <summary>Alvo de uma ação sem seletor numa lista de evento.</summary>
    public enum DefaultTarget
    {
        /// <summary>Os alvos do evento que disparou a lista.</summary>
        EventTargets,
        /// <summary>O ASC dono da lista: o dono da habilidade ou o ASC que tem o efeito.</summary>
        Owner,
    }

    /// <summary>
    /// Marca uma lista de <see cref="GameplayAction"/> como evento de um dono (habilidade, efeito, projétil). O Inspector
    /// mostra uma aba por evento, com <see cref="Label"/> no título.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ActionEventAttribute : Attribute
    {
        public ActionEventAttribute(string label, DefaultTarget defaultTarget, bool hasEventTargets)
        {
            Label = label;
            DefaultTarget = defaultTarget;
            HasEventTargets = hasEventTargets;
        }

        /// <summary>Nome do evento, como "Ao ser aplicado".</summary>
        public string Label { get; }

        /// <summary>Alvo das ações da lista que não têm seletor.</summary>
        public DefaultTarget DefaultTarget { get; }

        /// <summary>Se o evento traz alvos próprios.</summary>
        public bool HasEventTargets { get; }
    }
}
