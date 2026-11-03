using System;
using SpellBook.Core.Inspector;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Abilities.Triggers
{
    /// <summary>
    /// Gatilho de uma habilidade (<see cref="GameplayAbility.Triggers"/>): quando a causa dele acontece no ASC, o ASC
    /// tenta ativar a habilidade pelo mesmo caminho e pela mesma ordem de checagens de <c>TryActivateAbility</c>.
    /// </summary>
    [Serializable]
    public abstract class AbilityTrigger
    {
        [Tooltip("Só dispara com o autocast da habilidade ligado.")]
        [SerializeField] private bool onlyWithAutocast;

        /// <summary>Só dispara com o autocast ligado. Desligado, o padrão, o gatilho dispara sempre.</summary>
        public bool OnlyWithAutocast
        {
            get => onlyWithAutocast;
            set => onlyWithAutocast = value;
        }

        // O ASC pergunta a cada gatilho do índice se a causa é a dele, sem alocar
        internal virtual bool ListensTo(TriggerCause cause, GameplayTag tag) => false;
    }

    internal enum TriggerCause
    {
        GameplayEvent,
        TagAdded,
        TagRemoved,
    }

    /// <summary>Dispara quando o ASC recebe o evento de gameplay <see cref="Tag"/> ou de uma tag filha dela.</summary>
    [Serializable, TypeCategory("Evento", Summary = "Ativa a habilidade quando o ASC recebe um evento de gameplay.")]
    public sealed class GameplayEventTrigger : AbilityTrigger
    {
        [SerializeField] private GameplayTag tag;

        /// <summary>Tag do evento. Inválida não dispara.</summary>
        public GameplayTag Tag
        {
            get => tag;
            set => tag = value;
        }

        internal override bool ListensTo(TriggerCause cause, GameplayTag eventTag)
        {
            return cause == TriggerCause.GameplayEvent && tag.IsValid && eventTag.Matches(tag);
        }
    }

    /// <summary>Dispara quando o ASC passa a ter <see cref="Tag"/> ou uma tag filha dela.</summary>
    [Serializable, TypeCategory("Tags", Summary = "Ativa a habilidade quando o ASC ganha uma tag.")]
    public sealed class TagAddedTrigger : AbilityTrigger
    {
        [SerializeField] private GameplayTag tag;

        /// <summary>Tag observada, com as filhas. Inválida não dispara.</summary>
        public GameplayTag Tag
        {
            get => tag;
            set => tag = value;
        }

        internal override bool ListensTo(TriggerCause cause, GameplayTag changed)
        {
            return cause == TriggerCause.TagAdded && tag.IsValid && changed.Matches(tag);
        }
    }

    /// <summary>Dispara quando o ASC deixa de ter <see cref="Tag"/> ou uma tag filha dela.</summary>
    [Serializable, TypeCategory("Tags", Summary = "Ativa a habilidade quando o ASC perde uma tag.")]
    public sealed class TagRemovedTrigger : AbilityTrigger
    {
        [SerializeField] private GameplayTag tag;

        /// <summary>Tag observada, com as filhas. Inválida não dispara.</summary>
        public GameplayTag Tag
        {
            get => tag;
            set => tag = value;
        }

        internal override bool ListensTo(TriggerCause cause, GameplayTag changed)
        {
            return cause == TriggerCause.TagRemoved && tag.IsValid && changed.Matches(tag);
        }
    }
}
