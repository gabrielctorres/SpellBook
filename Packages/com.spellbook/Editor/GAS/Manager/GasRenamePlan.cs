using System;
using System.Collections.Generic;
using System.IO;
using SpellBook.Core.Editor;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>Um derivado que muda de nome junto com o alvo, se estiver marcado.</summary>
    internal sealed class GasRenameItem
    {
        public GasRenameItem(Object asset, string newName) => (Asset, NewName) = (asset, newName);

        public Object Asset { get; }

        public string NewName { get; }

        public bool Checked { get; internal set; } = true;
    }

    /// <summary>
    /// Renomear do gerenciador (CA12 e CA13). Uma habilidade leva junto os efeitos e as tags do conjunto
    /// (<c>GE_Fireball_Cost</c>, <c>Ability.Fireball</c>, <c>Cooldown.Fireball</c>); uma tag leva as filhas
    /// (<c>Ability</c> → <c>Skill</c> leva <c>Ability.IceBolt</c> a <c>Skill.IceBolt</c>). Todos vêm marcados, e o
    /// designer desmarca qualquer um. O <see cref="AssetDatabase.RenameAsset"/> mantém o GUID, então nenhuma referência
    /// quebra. Sem UI.
    /// </summary>
    internal sealed class GasRenamePlan
    {
        private readonly List<GasRenameItem> _items = new List<GasRenameItem>();
        private readonly HashSet<string> _tagNames = new HashSet<string>();
        private Object _target;
        private string _name;

        private GasRenamePlan() { }

        /// <summary>O nome novo do alvo.</summary>
        public string NewName { get; private set; }

        public IReadOnlyList<GasRenameItem> Items => _items;

        /// <summary>Quantos assets mudam de nome: o alvo e os marcados.</summary>
        public int Count => 1 + _items.FindAll(i => i.Checked).Count;

        /// <summary>A primeira mensagem que impede renomear; <c>null</c> se pode.</summary>
        public string Problem { get; private set; }

        /// <summary>
        /// O plano do alvo com o nome digitado: o nome do conjunto numa habilidade (<c>Firebolt</c>), o último trecho
        /// numa tag (<c>Heavy</c>) e o nome inteiro nos outros assets.
        /// </summary>
        public static GasRenamePlan Build(Object target, string name)
        {
            string setName = GasSetName.Of(target);
            var plan = new GasRenamePlan { _target = target, _name = name };
            plan.NewName = GasSetName.Rule(target, setName).Replace(AbilityTemplate.NameToken, name);
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GameplayTagDefinition)))
            {
                var tag = AssetDatabase.LoadAssetAtPath<GameplayTagDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (tag == null) continue;
                plan._tagNames.Add(tag.name);
                if (target is GameplayTagDefinition parent && tag != parent && tag.Matches(parent) && tag.name.StartsWith(parent.name + ".", StringComparison.Ordinal))
                {
                    plan._items.Add(new GasRenameItem(tag, plan.NewName + tag.name.Substring(parent.name.Length)));
                }
            }

            if (target is GameplayAbility ability)
            {
                foreach (Object item in Derived(ability, setName))
                {
                    plan._items.Add(new GasRenameItem(item, GasSetName.Rule(item, setName).Replace(AbilityTemplate.NameToken, name)));
                }
            }

            plan.Check();
            return plan;
        }

        public void SetChecked(int index, bool value)
        {
            _items[index].Checked = value;
            Check();
        }

        /// <summary>Renomeia o alvo e os marcados. Se um passo falha, volta os nomes já trocados e relança.</summary>
        /// <exception cref="InvalidOperationException">O plano tem um problema, ou a Unity recusou um nome.</exception>
        public void Apply()
        {
            if (Problem != null) throw new InvalidOperationException(Problem);
            var done = new List<(Object asset, string oldName)>();
            try
            {
                foreach ((Object asset, string newName) in Changes())
                {
                    string oldName = asset.name;
                    string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(asset), newName);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                    done.Add((asset, oldName));
                }
            }
            catch
            {
                for (int i = done.Count - 1; i >= 0; i--) AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(done[i].asset), done[i].oldName);
                throw;
            }
        }

        // Os efeitos e as tags que a habilidade usa, e as tags que esses efeitos usam, em Assets/ e com o nome do conjunto.
        private static List<Object> Derived(GameplayAbility ability, string setName)
        {
            var derived = new List<Object>();
            List<Object> used = ReferenceFinder.FindUses(ability);
            foreach (Object item in used.ToArray())
            {
                if (item is GameplayEffect effect) used.AddRange(ReferenceFinder.FindUses(effect));
            }

            foreach (Object item in used)
            {
                bool kind = item is GameplayEffect || item is GameplayTagDefinition;
                if (kind && !derived.Contains(item) && AssetDatabase.GetAssetPath(item).StartsWith("Assets/", StringComparison.Ordinal)
                    && GasSetName.Contains(item, setName))
                {
                    derived.Add(item);
                }
            }

            return derived;
        }

        private IEnumerable<(Object asset, string newName)> Changes()
        {
            yield return (_target, NewName);
            foreach (GasRenameItem item in _items)
            {
                if (item.Checked) yield return (item.Asset, item.NewName);
            }
        }

        private void Check()
        {
            Problem = AbilitySetPlan.CheckName(_name);
            if (Problem == null && NewName == _target.name) Problem = "Digite um nome diferente do atual.";
            foreach ((Object asset, string newName) in Changes())
            {
                if (Problem != null) return;
                string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset))?.Replace('\\', '/');
                if (asset is GameplayTagDefinition && _tagNames.Contains(newName)) Problem = $"Já existe a tag {newName}. Escolha outro nome.";
                else if (File.Exists($"{folder}/{newName}.asset")) Problem = $"Já existe um asset {newName} nesta pasta. Escolha outro nome.";
            }
        }
    }
}
