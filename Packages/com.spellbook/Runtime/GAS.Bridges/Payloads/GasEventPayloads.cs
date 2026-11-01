using System;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Dados de um evento de efeito: aplicado, removido, pilhas mudaram ou bloqueado.</summary>
    [Serializable]
    public struct EffectEventData
    {
        /// <summary>ASC que recebeu o efeito.</summary>
        public AbilitySystemComponent Owner;
        /// <summary>ASC que aplicou o efeito.</summary>
        public AbilitySystemComponent Source;
        /// <summary>Efeito do evento.</summary>
        public GameplayEffect Effect;
        /// <summary>Pilhas depois do evento.</summary>
        public int Stacks;
        /// <summary>Pilhas antes do evento.</summary>
        public int PreviousStacks;
        /// <summary>Motivo da remoção; só tem sentido no evento de remoção.</summary>
        public EffectRemovalReason RemovalReason;
        /// <summary>Desfecho da aplicação; só tem sentido nos eventos de aplicação e bloqueio.</summary>
        public EffectApplicationResult Result;
    }

    /// <summary>Dados de um evento de habilidade: ativada, terminada, falhou, concedida ou removida.</summary>
    [Serializable]
    public struct AbilityEventData
    {
        /// <summary>ASC dono da habilidade.</summary>
        public AbilitySystemComponent Owner;
        /// <summary>Habilidade do evento.</summary>
        public GameplayAbility Ability;
        /// <summary>Nível da habilidade no evento.</summary>
        public int Level;
        /// <summary>A habilidade terminou cancelada; só tem sentido no evento de fim.</summary>
        public bool Cancelled;
        /// <summary>Motivo da falha; só tem sentido no evento de falha.</summary>
        public AbilityActivationResult FailureReason;
        /// <summary>Tag do motivo da falha (<see cref="ActivationFailure.ReasonTag"/>); inválida quando não há.</summary>
        public GameplayTag FailureTag;
        /// <summary>Chave de texto do motivo da falha (<see cref="ActivationFailure.ReasonKey"/>); nula quando não há.</summary>
        public string FailureKey;
    }

    /// <summary>Dados da mudança do valor atual de um atributo.</summary>
    [Serializable]
    public struct AttributeChangedData
    {
        /// <summary>ASC dono do atributo.</summary>
        public AbilitySystemComponent Owner;
        /// <summary>Atributo que mudou.</summary>
        public AttributeDefinition Attribute;
        /// <summary>Valor antes da mudança.</summary>
        public float OldValue;
        /// <summary>Valor depois da mudança.</summary>
        public float NewValue;
        /// <summary><see cref="NewValue"/> menos <see cref="OldValue"/>; negativo quando o valor caiu.</summary>
        public float Delta;

        /// <summary>Monta os dados e grava <see cref="Delta"/> como <c>newValue - oldValue</c>.</summary>
        public AttributeChangedData(AbilitySystemComponent owner, AttributeDefinition attribute, float oldValue, float newValue)
        {
            Owner = owner;
            Attribute = attribute;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
        }
    }

    /// <summary>Dados de uma tag que entrou ou saiu do ASC.</summary>
    [Serializable]
    public struct TagChangedData
    {
        /// <summary>ASC dono da tag.</summary>
        public AbilitySystemComponent Owner;
        /// <summary>Tag que mudou.</summary>
        public GameplayTag Tag;
        /// <summary>A tag entrou (<c>true</c>) ou saiu (<c>false</c>).</summary>
        public bool Added;
        /// <summary>Contagem da tag depois da mudança.</summary>
        public int Count;
    }

    /// <summary>Dados de um bloqueio genérico, informado por <c>AbilitySystemComponent.ReportBlocked</c>.</summary>
    [Serializable]
    public struct BlockedEventData
    {
        /// <summary>ASC que bloqueou.</summary>
        public AbilitySystemComponent Owner;
        /// <summary>Tag do motivo do bloqueio.</summary>
        public GameplayTag Reason;
        /// <summary>ASC de origem da ação bloqueada.</summary>
        public AbilitySystemComponent Source;
    }
}
