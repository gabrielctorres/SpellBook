#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SpellBook.Testing
{
    /// <summary>
    /// Cria objetos do GAS para testes sem nenhum asset em disco. Tudo o que a factory cria é destruído no <see cref="Dispose"/>.
    /// Campos privados serializados são preenchidos por <c>SerializedObject</c>, como o Inspector faria.
    /// O <see cref="CreateAsc"/> depende do <c>Awake</c>, então é para testes PlayMode. Só compila no Editor (SerializedObject).
    /// </summary>
    public sealed class GasFactory : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly Dictionary<GameplayTag, GameplayTagDefinition> _tagDefinitions =
            new Dictionary<GameplayTag, GameplayTagDefinition>();

        /// <summary>ASC ativo com um <see cref="AttributeSet"/> que começa com os atributos dados.</summary>
        public AbilitySystemComponent CreateAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            var go = new GameObject("ASC");
            go.SetActive(false);
            _created.Add(go);

            var asc = go.AddComponent<AbilitySystemComponent>();
            AttributeSet set = go.GetComponent<AttributeSet>();
            Modify(set, so =>
            {
                SerializedProperty list = so.FindProperty("startingAttributes");
                list.arraySize = attributes.Length;
                for (int i = 0; i < attributes.Length; i++)
                {
                    SerializedProperty item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.Definition)).objectReferenceValue =
                        attributes[i].Definition;
                    item.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.InitialValue)).floatValue =
                        attributes[i].InitialValue;
                }
            });

            go.SetActive(true);
            return asc;
        }

        /// <summary>Tag nova; o <paramref name="parent"/>, se houver, precisa ter sido criado por esta factory.</summary>
        public GameplayTag CreateTag(string name, GameplayTag parent = default)
        {
            GameplayTagDefinition parentDefinition = null;
            if (parent.IsValid && !_tagDefinitions.TryGetValue(parent, out parentDefinition))
            {
                throw new ArgumentException($"A tag pai '{parent}' não foi criada por esta factory.", nameof(parent));
            }

            var definition = Track(ScriptableObject.CreateInstance<GameplayTagDefinition>(), name);
            Modify(definition, so => so.FindProperty("parent").objectReferenceValue = parentDefinition);

            var tag = new GameplayTag(definition);
            _tagDefinitions[tag] = definition;
            return tag;
        }

        public GameplayEffect CreateEffect(
            EffectDurationPolicy durationPolicy = EffectDurationPolicy.Instant,
            float duration = 0f,
            params GameplayTag[] grantedTags)
        {
            var effect = Track(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Test");
            effect.DurationPolicy = durationPolicy;
            effect.Duration = duration;
            effect.Modifiers = new List<AttributeEffectModifier>();
            effect.PeriodicModifiers = new List<AttributeEffectModifier>();
            effect.GrantedTags = new List<GameplayTag>(grantedTags);
            effect.ApplicationRequirements = new List<GameplayTag>();
            effect.ApplicationImmunityTags = new List<GameplayTag>();
            return effect;
        }

        public GameplayAbility CreateAbility(GameplayTag abilityTag = default, params AbilityAction[] actions)
        {
            var ability = Track(ScriptableObject.CreateInstance<GameplayAbility>(), "GA_Test");
            ability.AbilityTag = abilityTag;
            ability.BlockedTags = new List<GameplayTag>();
            ability.RequiredTags = new List<GameplayTag>();
            ability.ActivationOwnedTags = new List<GameplayTag>();
            ability.CancelAbilitiesWithTag = new List<GameplayTag>();
            ability.BlockAbilitiesWithTag = new List<GameplayTag>();
            ability.CancelledByTags = new List<GameplayTag>();
            ability.Actions = new List<AbilityAction>(actions);
            return ability;
        }

        public void Dispose()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_created[i]);
                else Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _tagDefinitions.Clear();
        }

        private T Track<T>(T asset, string name) where T : Object
        {
            asset.name = name;
            _created.Add(asset);
            return asset;
        }

        private static void Modify(Object target, Action<SerializedObject> edit)
        {
            using (var so = new SerializedObject(target))
            {
                edit(so);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
