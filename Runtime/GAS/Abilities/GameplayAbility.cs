using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;
using SpellBook.GAS.Tags;
using SpellBook.GAS.Attributes;
using Sirenix.OdinInspector;

namespace SpellBook.GAS.Abilities
{
    [CreateAssetMenu(fileName = "GA_NewAbility", menuName = "GAS/Gameplay Ability")]
    public class GameplayAbility : ScriptableObject, ICooldownModifier
    {
        [TabGroup("AbilityConfig", "Identity")]
        [PreviewField(60, ObjectFieldAlignment.Left)]
        [HideLabel]
        [HorizontalGroup("AbilityConfig/Identity/Horizontal")]
        public Sprite Icon;

        [TabGroup("AbilityConfig", "Identity")]
        [VerticalGroup("AbilityConfig/Identity/Horizontal/Right")]
        public string DisplayName;

        [TabGroup("AbilityConfig", "Identity")]
        [VerticalGroup("AbilityConfig/Identity/Horizontal/Right")]
        public GameplayTag AbilityTag;

        [TabGroup("AbilityConfig", "Activation")]
        [BoxGroup("AbilityConfig/Activation/Cooldown")]
        [InlineEditor]
        public SpellBook.GAS.Effects.GameplayEffect CooldownEffect;

        [TabGroup("AbilityConfig", "Activation")]
        [BoxGroup("AbilityConfig/Activation/Cooldown")]
        public AttributeDefinition _cooldownReductionAttribute;

        [TabGroup("AbilityConfig", "Activation")]
        [Title("Tags Requirements")]
        public List<GameplayTag> BlockedTags;
        public List<GameplayTag> RequiredTags;

        [TabGroup("AbilityConfig", "Execution")]
        [SerializeReference]
        [ListDrawerSettings(ShowIndexLabels = true, Expanded = true)]
        [Searchable]
        public List<AbilityAction> Actions = new List<AbilityAction>();

        public AttributeDefinition CooldownReductionAttribute => _cooldownReductionAttribute;

        public bool CanActivate(AbilitySystemComponent source)
        {
            if (source.HasAnyTag(BlockedTags)) return false;
            if (RequiredTags.Count > 0 && !source.HasAllTags(RequiredTags)) return false;
            
            // Check Cooldown
            if (CooldownEffect != null)
            {
                foreach (var tag in CooldownEffect.GrantedTags)
                {
                    if (source.HasTag(tag)) return false;
                }
            }

            return true;
        }

        public IEnumerator Activate(AbilitySystemComponent source, Vector3 targetPosition)
        {
            if (!CanActivate(source)) yield break;

            // Apply Cooldown at start of activation
            if (CooldownEffect != null)
            {
                float finalDuration = CooldownEffect.Duration;
                
                if (CooldownReductionAttribute != null)
                {
                    float cdr = source.GetAttributeValue(CooldownReductionAttribute);
                    // Abordagem de porcentagem: CDR 0.20 significa 20% de redução
                    float multiplier = 1f - Mathf.Clamp01(cdr);
                    finalDuration = Mathf.Max(0.1f, finalDuration * multiplier);
                }

                source.ApplyEffect(CooldownEffect, finalDuration);
            }

            // Dispara evento para UI DEPOIS de aplicar o efeito, para que a UI leia o tempo correto
            SpellBook.Events.EventManager.Trigger(new SpellBook.Events.AbilityActivatedEvent(this, source));

            var context = new AbilityContext(source, targetPosition);

            foreach (var action in Actions)
            {
                if (action == null) continue;
                yield return action.Execute(context);
            }
        }
    }
}


