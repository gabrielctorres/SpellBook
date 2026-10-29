using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using SpellBook.GAS.Core;
using SpellBook.GAS.Tags;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Dados de uma habilidade. A execução é feita pelo AbilitySystemComponent (TryActivateAbility).
    /// </summary>
    [CreateAssetMenu(fileName = "GA_NewAbility", menuName = "GAS/Gameplay Ability")]
    public class GameplayAbility : ScriptableObject
    {
        [Header("Identidade")]
        public Sprite Icon;

        public string DisplayName;

        public GameplayTag AbilityTag;

        public AbilityDescription Description;

        [Header("Ativação")]
        public GameplayEffect CooldownEffect;

        [FormerlySerializedAs("_cooldownReductionAttribute")]
        public AttributeDefinition CooldownReductionAttribute;

        [Tooltip("Efeito Instant aplicado ao ativar (ex: Mana Add -20). A ativação falha se o atributo ficaria abaixo do MinValue.")]
        public GameplayEffect CostEffect;

        [Header("Tags")]
        [Tooltip("Não ativa se o dono tiver alguma destas tags.")]
        public List<GameplayTag> BlockedTags;

        [Tooltip("Só ativa se o dono tiver todas estas tags.")]
        public List<GameplayTag> RequiredTags;

        [Tooltip("Tags concedidas ao dono enquanto a habilidade está ativa (ex: State.Casting).")]
        public List<GameplayTag> ActivationOwnedTags;

        [Tooltip("Ao ativar, cancela habilidades ativas cujo AbilityTag corresponda a uma destas.")]
        public List<GameplayTag> CancelAbilitiesWithTag;

        [Tooltip("Enquanto ativa, impede a ativação de habilidades cujo AbilityTag corresponda a uma destas.")]
        public List<GameplayTag> BlockAbilitiesWithTag;

        [Tooltip("Cancela esta habilidade se o dono receber uma destas tags durante a execução (ex: State.Stunned).")]
        public List<GameplayTag> CancelledByTags;

        [Header("Execução")]
        [SerializeReference]
        public List<AbilityAction> Actions = new List<AbilityAction>();

        /// <summary>
        /// Verifica se a habilidade pode ser ativada pelo ASC, informando o motivo em caso de falha.
        /// </summary>
        public bool CanActivate(AbilitySystemComponent asc, out AbilityActivationResult reason)
        {
            reason = asc != null ? asc.GetActivationResult(this) : AbilityActivationResult.InvalidAbility;
            return reason == AbilityActivationResult.Success;
        }

        public bool CanActivate(AbilitySystemComponent asc) => CanActivate(asc, out _);

        /// <summary>
        /// Duração do cooldown já considerando o atributo de redução (CDR 0.20 = 20% de redução).
        /// </summary>
        public float GetCooldownDuration(AbilitySystemComponent asc)
        {
            if (CooldownEffect == null) return 0f;

            float duration = CooldownEffect.Duration;
            if (CooldownReductionAttribute != null && asc != null)
            {
                float cdr = asc.GetAttributeValue(CooldownReductionAttribute);
                duration = Mathf.Max(0.1f, duration * (1f - Mathf.Clamp01(cdr)));
            }
            return duration;
        }

        public string GetDescription(AbilitySystemComponent source = null)
        {
            if (Description == null) return string.Empty;
            return Description.GetFullDescription(this, source);
        }
    }
}
