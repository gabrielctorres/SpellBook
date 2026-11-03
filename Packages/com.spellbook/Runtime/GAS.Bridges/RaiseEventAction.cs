using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Dispara um canal de eventos uma vez e termina na mesma chamada. O payload é o fixo da emissão ou, com outra
    /// <see cref="GasPayloadSource"/>, um valor lido do <see cref="ActionContext"/> na hora. Serve nas listas da habilidade
    /// e nas do efeito; no efeito, as fontes leem o efeito, o nível e o evento guardados no contexto. O instigador aparece
    /// como quem disparou. A ação não dispara nada sem emissão, sem canal, com fonte que não serve para o canal ou para a
    /// lista, ou sem valor para enviar (sem conjurador, sem o atributo, sem alvo válido, sem o modificador do índice). A
    /// validação acusa o que é configuração.
    /// </summary>
    [HelpURL(DocUrls.Gas + "#raiseeventaction")]
    [Serializable, TypeCategory("Eventos", Summary = "Dispara um canal de eventos com um payload fixo, da habilidade ou do efeito.")]
    public sealed class RaiseEventAction : GameplayAction
    {
        [SerializeReference, Tooltip("Canal disparado e payload enviado.")]
        private EventEmission emission;

        [SerializeField, Tooltip("De onde vem o payload. Fixed usa o payload da emissão; as outras fontes o ignoram.")]
        private GasPayloadSource payloadSource;

        [SerializeField, Tooltip("Atributo lido pela fonte AttributeValue: do conjurador ou, numa lista do efeito, do dono do efeito.")]
        private AttributeDefinition attribute;

        [SerializeField, Min(0), Tooltip("Modificador lido pela fonte ModifierMagnitude: de Periodic Modifiers no tique e de Modifiers nos outros eventos.")]
        private int modifierIndex;

        [NonSerialized] private bool _warnedIndex;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (emission == null) return ActionStatus.Done;

            if (payloadSource == GasPayloadSource.Fixed)
            {
                emission.Raise(context.Instigator);
            }
            else if (emission.Channel != null
                     && GasPayloadSourceUtility.Supports(payloadSource, emission.PayloadType, context.HasEffect))
            {
                if (context.HasEffect) RaiseFromEffect(context);
                else RaiseDynamic(context);
            }

            return ActionStatus.Done;
        }

        public override void Validate(ValidationResult result)
        {
            if (emission == null)
            {
                result.AddError("RaiseEventAction sem emissão: escolha o canal.");
                return;
            }

            emission.Validate(result);

            // O asset validado diz onde a ação está: numa lista do efeito ou fora delas
            bool inEffectList = result.Context is GameplayEffect;
            if (emission.Channel != null && !GasPayloadSourceUtility.Supports(payloadSource, emission.PayloadType, inEffectList))
            {
                string place = inEffectList ? "numa lista do efeito" : "fora das listas do efeito";
                result.AddError(
                    $"RaiseEventAction: a fonte {payloadSource} não serve para um canal de {emission.PayloadType.Name} {place}.",
                    nameof(payloadSource));
            }

            if (payloadSource == GasPayloadSource.ModifierMagnitude && modifierIndex < 0)
            {
                result.AddError("RaiseEventAction com modifierIndex negativo: use 0 ou mais.", nameof(modifierIndex));
            }

            if (payloadSource == GasPayloadSource.AttributeValue && attribute == null)
            {
                result.AddError("RaiseEventAction com fonte AttributeValue sem atributo: escolha o atributo.", nameof(attribute));
            }
        }

        // Lado do efeito. Só é chamado com a fonte compatível com o canal numa lista do efeito.
        private void RaiseFromEffect(ActionContext context)
        {
            AbilitySystemComponent sender = context.Instigator;
            AbilitySystemComponent owner = context.Owner;
            int stacks = context.EffectInstance != null ? context.EffectInstance.CurrentStacks : 1;
            switch (payloadSource)
            {
                case GasPayloadSource.AttributeValue:
                    if (owner != null && attribute != null && owner.TryGetAttributeValue(attribute, out float value))
                    {
                        ((EventChannel<float>)emission.Channel).Raise(value, sender);
                    }

                    break;
                case GasPayloadSource.ModifierMagnitude:
                    if (TryGetModifierMagnitude(context, out float magnitude))
                    {
                        ((EventChannel<float>)emission.Channel).Raise(magnitude, sender);
                    }

                    break;
                case GasPayloadSource.Stacks:
                    ((EventChannel<float>)emission.Channel).Raise(stacks, sender);
                    break;
                case GasPayloadSource.EffectName:
                    if (context.EffectDefinition != null)
                    {
                        ((EventChannel<string>)emission.Channel).Raise(context.EffectDefinition.name, sender);
                    }

                    break;
                case GasPayloadSource.SourceObject:
                    if (sender != null)
                    {
                        ((EventChannel<GameObject>)emission.Channel).Raise(sender.gameObject, sender);
                    }

                    break;
                case GasPayloadSource.TargetObject:
                    if (owner != null)
                    {
                        ((EventChannel<GameObject>)emission.Channel).Raise(owner.gameObject, sender);
                    }

                    break;
                case GasPayloadSource.EventData:
                    var data = new EffectEventData
                    {
                        Owner = owner,
                        Source = sender,
                        Effect = context.EffectDefinition,
                        Stacks = stacks,
                        PreviousStacks = stacks
                    };
                    ((EventChannel<EffectEventData>)emission.Channel).Raise(data, sender);
                    break;
            }
        }

        // O modificador do índice, de PeriodicModifiers no tique e de Modifiers nos outros eventos, no nível do efeito.
        // Sem ele, avisa uma vez por ação e não dispara.
        private bool TryGetModifierMagnitude(ActionContext context, out float magnitude)
        {
            magnitude = 0f;
            GameplayEffect effect = context.EffectDefinition;
            bool tick = context.EffectEvent == EffectEvent.Tick;
            List<AttributeEffectModifier> modifiers = effect == null ? null : tick ? effect.PeriodicModifiers : effect.Modifiers;
            ModifierValueSource valueSource = modifiers != null && modifierIndex >= 0 && modifierIndex < modifiers.Count
                ? modifiers[modifierIndex]?.ValueSource
                : null;

            if (valueSource == null)
            {
                if (!_warnedIndex)
                {
                    _warnedIndex = true;
                    string list = tick ? "Periodic Modifiers" : "Modifiers";
                    string effectName = effect != null ? effect.name : "(sem efeito)";
                    Debug.LogWarning(
                        $"[GAS] RaiseEventAction: modifierIndex {modifierIndex} não tem modificador em {list} de {effectName}; o canal não foi disparado.");
                }

                return false;
            }

            magnitude = valueSource.GetValue(context.Effect);
            return true;
        }

        // Lado da habilidade. Só é chamado com a fonte compatível com o canal, então cada cast abaixo acerta o tipo.
        private void RaiseDynamic(ActionContext context)
        {
            AbilitySystemComponent source = context.Instigator;
            switch (payloadSource)
            {
                case GasPayloadSource.AttributeValue:
                    if (source != null && attribute != null && source.TryGetAttributeValue(attribute, out float value))
                    {
                        ((EventChannel<float>)emission.Channel).Raise(value, source);
                    }

                    break;
                case GasPayloadSource.AbilityName:
                    if (context.Ability != null)
                    {
                        ((EventChannel<string>)emission.Channel).Raise(context.Ability.name, source);
                    }

                    break;
                case GasPayloadSource.SourceObject:
                    if (source != null)
                    {
                        ((EventChannel<GameObject>)emission.Channel).Raise(source.gameObject, source);
                    }

                    break;
                case GasPayloadSource.TargetObject:
                    List<AbilitySystemComponent> targets = context.EventTargets;
                    targets.RemoveAll(t => t == null);
                    if (targets.Count > 0)
                    {
                        ((EventChannel<GameObject>)emission.Channel).Raise(targets[0].gameObject, source);
                    }

                    break;
                case GasPayloadSource.EventData:
                    var data = new AbilityEventData
                    {
                        Owner = source,
                        Ability = context.Ability,
                        Level = context.Level,
                        Cancelled = context.IsCancelled
                    };
                    ((EventChannel<AbilityEventData>)emission.Channel).Raise(data, source);
                    break;
            }
        }
    }
}
