using System;
using System.Collections.Generic;
using System.IO;
using SpellBook.Core.Editor;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Nome do conjunto de um asset e a regra que o troca no nome de cada item. Numa habilidade, o nome do conjunto é o
    /// último trecho da <c>AbilityTag</c> (<c>Ability.Fireball</c> dá <c>Fireball</c>) se o nome do asset o contém; numa
    /// tag, o último trecho; nos outros assets, o nome inteiro.
    /// </summary>
    internal static class GasSetName
    {
        public static string Of(Object asset)
        {
            if (asset is GameplayTagDefinition) return Last(asset.name);
            string tag = asset is GameplayAbility ability && ability.AbilityTag.IsValid ? Last(ability.AbilityTag.Name) : null;
            return !string.IsNullOrEmpty(tag) && asset.name.Contains(tag) ? tag : asset.name;
        }

        /// <summary>A regra do nome do item, com <see cref="AbilityTemplate.NameToken"/>. Numa tag, só o último trecho muda.</summary>
        public static string Rule(Object item, string setName)
        {
            int start = item is GameplayTagDefinition ? item.name.LastIndexOf('.') + 1 : 0;
            string last = item.name.Substring(start);
            string rule = last.Contains(setName) ? last.Replace(setName, AbilityTemplate.NameToken) : last + "_" + AbilityTemplate.NameToken;
            return item.name.Substring(0, start) + rule;
        }

        public static bool Contains(Object item, string setName) =>
            (item is GameplayTagDefinition ? Last(item.name) : item.name).Contains(setName);

        private static string Last(string name) => name.Substring(name.LastIndexOf('.') + 1);
    }

    /// <summary>
    /// Duplicar do gerenciador (CA11, Q11), pelo motor do assistente. A cópia de uma habilidade tem custo, recarga e tags
    /// próprios: a tag da habilidade e as tags da recarga (<c>Cooldown.Fireball</c>), porque compartilhar a recarga faria
    /// as duas recarregarem juntas. Os outros efeitos ficam compartilhados. A cópia de um efeito é só o efeito.
    /// </summary>
    internal static class GasDuplicate
    {
        public static bool CanDuplicate(Object asset) => asset is GameplayAbility || asset is GameplayEffect;

        private const int MaxSuggestions = 1000;

        /// <summary>
        /// O primeiro nome <c>Fireball_2</c>, <c>Fireball_3</c>… que não colide nem reutiliza uma tag nova. Só a colisão
        /// faz tentar o próximo número: com outro problema (nome inválido, pasta fora de Assets), devolve o nome, e o
        /// <see cref="Problem"/> do plano diz o motivo. Desiste depois de <see cref="MaxSuggestions"/> tentativas.
        /// </summary>
        public static string Suggest(Object source)
        {
            string setName = GasSetName.Of(source);
            string name = null;
            for (int i = 2; i < MaxSuggestions + 2; i++)
            {
                name = $"{setName}_{i}";
                AbilitySetPlan plan = Plan(source, name);
                if (Problem(plan) == null || !Taken(plan)) return name;
            }

            return name;
        }

        // O nome já está em uso: um asset colide na pasta ou uma tag nova já existe.
        private static bool Taken(AbilitySetPlan plan)
        {
            foreach (PlannedAsset asset in plan.Assets)
            {
                if (asset.Status == PlannedStatus.Collision) return true;
            }

            foreach (PlannedTag tag in plan.Tags)
            {
                if (tag.Status == PlannedStatus.Collision || (tag.Source != null && tag.Status == PlannedStatus.Exists)) return true;
            }

            return false;
        }

        /// <summary>O plano da cópia, na pasta do original.</summary>
        public static AbilitySetPlan Plan(Object source, string name)
        {
            string setName = GasSetName.Of(source);
            var assets = new List<(ScriptableObject, string)>();
            var tags = new List<(GameplayTagDefinition, string)>();
            void AddAsset(ScriptableObject asset)
            {
                if (asset != null) assets.Add((asset, GasSetName.Rule(asset, setName)));
            }

            AddAsset((ScriptableObject)source);
            if (source is GameplayAbility ability)
            {
                AddAsset(ability.CostEffect);
                AddAsset(ability.CooldownEffect);
                var renewed = new List<GameplayTag> { ability.AbilityTag };
                if (ability.CooldownEffect != null && ability.CooldownEffect.GrantedTags != null) renewed.AddRange(ability.CooldownEffect.GrantedTags);
                foreach (GameplayTag tag in renewed)
                {
                    GameplayTagDefinition definition = tag.GetDefinition();
                    if (definition != null) tags.Add((definition, GasSetName.Rule(definition, setName)));
                }
            }

            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source))?.Replace('\\', '/');
            return AbilitySetPlan.Build(name, folder, assets, tags);
        }

        /// <summary>O problema do plano, ou a tag nova que já existe e seria compartilhada; <c>null</c> se pode criar.</summary>
        public static string Problem(AbilitySetPlan plan)
        {
            if (plan.Problem != null) return plan.Problem;
            foreach (PlannedTag tag in plan.Tags)
            {
                if (tag.Source != null && tag.Status == PlannedStatus.Exists) return $"Já existe a tag {tag.Name}. Escolha outro nome.";
            }

            return null;
        }

        /// <summary>Os efeitos que a cópia de uma habilidade compartilha com a original.</summary>
        public static List<GameplayEffect> Shared(Object source)
        {
            var shared = new List<GameplayEffect>();
            if (!(source is GameplayAbility ability)) return shared;
            foreach (Object used in ReferenceFinder.FindUses(ability))
            {
                if (used is GameplayEffect effect && effect != ability.CostEffect && effect != ability.CooldownEffect) shared.Add(effect);
            }

            return shared;
        }

        /// <summary>Grava a cópia e devolve o asset principal, que fica selecionado.</summary>
        /// <exception cref="InvalidOperationException">O plano tem um problema.</exception>
        public static Object Create(AbilitySetPlan plan)
        {
            string problem = Problem(plan);
            if (problem != null) throw new InvalidOperationException(problem);
            return AbilitySetCreator.Create(plan).Main;
        }
    }
}
