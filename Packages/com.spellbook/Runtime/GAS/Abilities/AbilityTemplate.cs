using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Modelo do assistente de criação: os assets modelo a copiar, a regra de nome de cada cópia e as tags a criar.
    /// É só dado. O pacote traz os seus modelos, e o projeto cria os dele com este mesmo tipo, sem código.
    /// </summary>
    [CreateAssetMenu(fileName = "New Ability Template", menuName = "SpellBook/GAS/Ability Template")]
    [HelpURL(DocUrls.Gas)]
    public sealed class AbilityTemplate : ScriptableObject, IValidatable
    {
        /// <summary>Trecho das regras de nome e de tag trocado pelo nome que o designer digita.</summary>
        public const string NameToken = "{Name}";

        /// <summary>Um asset modelo e a regra do nome da cópia.</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Asset copiado: uma habilidade, um efeito ou outro ScriptableObject do conjunto.")]
            [SerializeField] private ScriptableObject source;

            [Tooltip("Nome da cópia, com {Name} no lugar do nome digitado. Exemplo: GA_{Name}.")]
            [SerializeField] private string nameRule;

            public ScriptableObject Source => source;

            public string NameRule => nameRule;
        }

        /// <summary>Uma tag do conjunto: a regra do nome e a tag-modelo que, nas cópias, vira a tag criada ou reutilizada.</summary>
        [Serializable]
        public sealed class TagEntry
        {
            [Tooltip("Tag que os assets modelo usam no lugar desta. Nas cópias, toda referência a ela aponta para a tag criada ou reutilizada. Vazio: a tag é criada sem ligação.")]
            [SerializeField] private GameplayTagDefinition source;

            [Tooltip("Nome da tag, com {Name}. O pai é o trecho antes do último ponto: Ability.{Name} tem o pai Ability.")]
            [SerializeField] private string rule;

            public GameplayTagDefinition Source => source;

            public string Rule => rule;
        }

        [Tooltip("Nome do modelo no assistente. Vazio: o nome do asset.")]
        [SerializeField] private string displayName;

        [Tooltip("Frase do que o modelo cria, mostrada no assistente.")]
        [SerializeField, TextArea] private string description;

        [Tooltip("Assets copiados. O primeiro é o principal, selecionado depois de criar.")]
        [SerializeField] private List<Entry> assets = new List<Entry>();

        [Tooltip("Tags a criar ou reutilizar, cada uma com a regra do nome e a tag-modelo que ela substitui nas cópias.")]
        [SerializeField] private List<TagEntry> tags = new List<TagEntry>();

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public string Description => description;

        public IReadOnlyList<Entry> Assets => assets;

        public IReadOnlyList<TagEntry> Tags => tags;

        /// <summary>Verdadeiro se o modelo tem asset e nenhum modelo ausente. Só os disponíveis aparecem no assistente.</summary>
        public bool IsAvailable
        {
            get
            {
                if (assets.Count == 0)
                {
                    return false;
                }

                foreach (Entry entry in assets)
                {
                    if (entry == null || entry.Source == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void Validate(ValidationResult result)
        {
            if (assets.Count == 0)
            {
                result.AddError("Modelo sem assets: acrescente ao menos o asset principal.", nameof(assets));
            }

            for (int i = 0; i < assets.Count; i++)
            {
                Entry entry = assets[i];
                string path = $"{nameof(assets)}.Array.data[{i}]";
                string rule = entry?.NameRule;
                if (entry == null || entry.Source == null)
                {
                    result.AddError($"Asset modelo ausente em \"{rule}\": o asset foi apagado ou o tipo dele não existe. " +
                        "Escolha outro asset.", path + ".source");
                }

                if (string.IsNullOrEmpty(rule) || !rule.Contains(NameToken))
                {
                    result.AddError($"Regra de nome sem {NameToken}: todas as cópias teriam o mesmo nome. Exemplo: GA_{NameToken}.",
                        path + ".nameRule");
                }
            }

            var sources = new HashSet<GameplayTagDefinition>();
            for (int i = 0; i < tags.Count; i++)
            {
                string path = $"{nameof(tags)}.Array.data[{i}]";
                string rule = tags[i]?.Rule;
                if (string.IsNullOrEmpty(rule) || !rule.Contains(NameToken))
                {
                    result.AddError($"Tag sem {NameToken}: todas as habilidades teriam a mesma tag. Exemplo: Ability.{NameToken}.",
                        path + ".rule");
                }

                GameplayTagDefinition source = tags[i]?.Source;
                if (source != null && !sources.Add(source))
                {
                    result.AddError($"A tag-modelo {source.name} já está em outra tag do modelo. Escolha uma tag-modelo por regra.",
                        path + ".source");
                }
            }
        }
    }
}
