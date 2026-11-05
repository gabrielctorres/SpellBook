using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SpellBook.GAS.Abilities;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>Situação de uma linha do plano: o item será criado, já existe e será reutilizado, ou colide.</summary>
    public enum PlannedStatus { New, Exists, Collision }

    /// <summary>Um asset do conjunto: o modelo, o nome e o caminho da cópia.</summary>
    public sealed class PlannedAsset
    {
        public ScriptableObject Source { get; internal set; }
        public string Name { get; internal set; }
        public string Path { get; internal set; }
        /// <summary>O asset selecionado depois de criar; é o primeiro do plano.</summary>
        public bool IsMain { get; internal set; }
        public PlannedStatus Status { get; internal set; }
    }

    /// <summary>Uma tag do conjunto, com o nome inteiro (<c>Ability.IceBolt</c>) e o pai (<c>Ability</c>).</summary>
    public sealed class PlannedTag
    {
        public string Name { get; internal set; }
        /// <summary>Nome do pai; <c>null</c> na raiz.</summary>
        public string ParentName { get; internal set; }
        /// <summary>Caminho da tag nova; vazio se ela já existe.</summary>
        public string Path { get; internal set; }
        public PlannedStatus Status { get; internal set; }
        /// <summary>A tag reutilizada, se já existe no projeto.</summary>
        public GameplayTagDefinition Existing { get; internal set; }
        /// <summary>A tag-modelo que, nas cópias, passa a apontar para esta tag; <c>null</c> se não há.</summary>
        public GameplayTagDefinition Source { get; internal set; }
    }

    /// <summary>
    /// O que a criação de um conjunto vai fazer, antes de fazer: o nome final de cada cópia, as tags novas e as
    /// reutilizadas, os prefabs mantidos e o motivo que impede a criação. Sem UI; o assistente, o Duplicar e o Renomear
    /// do gerenciador o montam e o mostram.
    /// </summary>
    public sealed class AbilitySetPlan
    {
        internal const string NoAssets = "O conjunto precisa de pelo menos um asset.";

        private static readonly Regex ValidName = new Regex("^[A-Za-z0-9_]+$");

        private readonly List<PlannedAsset> _assets = new List<PlannedAsset>();
        private readonly List<PlannedTag> _tags = new List<PlannedTag>();
        private readonly List<Object> _references = new List<Object>();

        private AbilitySetPlan() { }

        public IReadOnlyList<PlannedAsset> Assets => _assets;

        public IReadOnlyList<PlannedTag> Tags => _tags;

        /// <summary>Prefabs que os modelos usam. Não são copiados: as cópias apontam para eles.</summary>
        public IReadOnlyList<Object> References => _references;

        /// <summary>A primeira mensagem que impede a criação; <c>null</c> se o plano pode ser criado.</summary>
        public string Problem { get; private set; }

        public bool CanCreate => Problem == null;

        /// <summary>A mensagem do nome digitado, ou <c>null</c> se ele serve.</summary>
        public static string CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Digite um nome.";
            return ValidName.IsMatch(name) ? null : "Use só letras, números e _.";
        }

        /// <summary>Plano do modelo: cada asset pela regra de nome dele e cada tag pela regra dela.</summary>
        public static AbilitySetPlan Build(AbilityTemplate template, string name, string folder)
        {
            var assets = new List<(ScriptableObject, string)>();
            foreach (AbilityTemplate.Entry entry in template.Assets) assets.Add((entry.Source, entry.NameRule));
            var tags = new List<(GameplayTagDefinition, string)>();
            foreach (AbilityTemplate.TagEntry entry in template.Tags) tags.Add((entry.Source, entry.Rule));
            return Build(name, folder, assets, tags);
        }

        /// <summary>
        /// Plano geral, sem modelo: cada par diz o asset a copiar (ou a tag-modelo a trocar) e a regra do nome, com
        /// <see cref="AbilityTemplate.NameToken"/>. O primeiro asset é o principal.
        /// </summary>
        public static AbilitySetPlan Build(string name, string folder, IReadOnlyList<(ScriptableObject source, string nameRule)> assets,
            IReadOnlyList<(GameplayTagDefinition source, string rule)> tags)
        {
            var plan = new AbilitySetPlan { Problem = CheckName(name) ?? CheckFolder(folder) };
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((ScriptableObject source, string nameRule) in assets)
            {
                var asset = new PlannedAsset { Source = source, Name = nameRule.Replace(AbilityTemplate.NameToken, name), IsMain = plan._assets.Count == 0 };
                asset.Path = $"{folder}/{asset.Name}.asset";
                asset.Status = plan.Claim(asset.Name, asset.Path, paths);
                if (source == null) plan.Problem ??= "O modelo tem um asset ausente. Rode SpellBook/Validar projeto para ver qual.";
                plan._assets.Add(asset);
                plan.CollectReferences(source);
            }

            if (plan._assets.Count == 0) plan.Problem ??= NoAssets;

            Dictionary<string, GameplayTagDefinition> existing = ProjectTags();
            foreach ((GameplayTagDefinition source, string rule) in tags)
            {
                PlannedTag tag = plan.AddTag(rule.Replace(AbilityTemplate.NameToken, name), folder, existing, paths);
                if (source != null) tag.Source = source;
            }

            return plan;
        }

        private static string CheckFolder(string folder)
        {
            bool inAssets = folder == "Assets" || (folder != null && folder.StartsWith("Assets/", StringComparison.Ordinal));
            return inAssets && AssetDatabase.IsValidFolder(folder) ? null : "Escolha uma pasta dentro de Assets.";
        }

        // Acrescenta a tag e, antes dela, os pais que ainda não estão no plano. Devolve a linha da tag.
        private PlannedTag AddTag(string tagName, string folder, Dictionary<string, GameplayTagDefinition> existing, HashSet<string> paths)
        {
            PlannedTag planned = _tags.Find(t => t.Name == tagName);
            if (planned != null) return planned;

            int dot = tagName.LastIndexOf('.');
            string parent = dot > 0 ? tagName.Substring(0, dot) : null;
            if (parent != null) AddTag(parent, folder, existing, paths);

            planned = new PlannedTag { Name = tagName, ParentName = parent };
            if (existing.TryGetValue(tagName, out GameplayTagDefinition found))
            {
                planned.Existing = found;
                planned.Status = PlannedStatus.Exists;
            }
            else
            {
                planned.Path = $"{folder}/{tagName}.asset";
                planned.Status = Claim(tagName, planned.Path, paths);
            }

            _tags.Add(planned);
            return planned;
        }

        // Novo se o caminho está livre no disco e no plano; senão, colisão, e a primeira vira o motivo do plano.
        private PlannedStatus Claim(string assetName, string path, HashSet<string> paths)
        {
            if (paths.Add(path) && !File.Exists(path)) return PlannedStatus.New;
            Problem ??= $"Já existe um asset {assetName} nesta pasta. Escolha outro nome.";
            return PlannedStatus.Collision;
        }

        private void CollectReferences(ScriptableObject source)
        {
            if (source == null) return;
            SerializedProperty property = new SerializedObject(source).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object value = property.objectReferenceValue;
                if ((value is GameObject || value is Component) && EditorUtility.IsPersistent(value) && !_references.Contains(value)) _references.Add(value);
            }
        }

        // Tags do projeto e dos pacotes pelo nome do asset; com nomes repetidos, vale o primeiro caminho em ordem.
        private static Dictionary<string, GameplayTagDefinition> ProjectTags()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GameplayTagDefinition))) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);

            var tags = new Dictionary<string, GameplayTagDefinition>();
            foreach (string path in paths)
            {
                var tag = AssetDatabase.LoadAssetAtPath<GameplayTagDefinition>(path);
                if (tag != null && !tags.ContainsKey(tag.name)) tags.Add(tag.name, tag);
            }

            return tags;
        }
    }
}
