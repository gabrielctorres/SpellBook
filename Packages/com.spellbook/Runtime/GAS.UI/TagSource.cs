using System;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>O que a <see cref="TagSource"/> confere no ASC dono.</summary>
    public enum TagSourceMode
    {
        /// <summary>Uma tag; a tag filha ativa satisfaz a pai.</summary>
        Tag,

        /// <summary>Uma consulta com as listas Todas, Alguma e Nenhuma.</summary>
        Query,
    }

    /// <summary>
    /// Fonte do indicador de tag (R9): acesa enquanto o ASC dono tem a <see cref="Tag"/> ou satisfaz a
    /// <see cref="Query"/>, pelo <c>OnTagChanged</c> do GAS, sem <c>Update</c>. Publica só quando o estado muda. A
    /// configuração vale a partir da próxima ligação.
    /// </summary>
    [Serializable, TypeCategory("Tags", Summary = "Indicador aceso enquanto o dono tem uma tag ou satisfaz uma consulta de tags.")]
    public sealed class TagSource : UISource<TagIndicatorModel>
    {
        [Tooltip("Confere uma tag ou uma consulta.")]
        [SerializeField] private TagSourceMode mode;

        [Tooltip("Tag conferida no modo Tag.")]
        [SerializeField] private GameplayTag tag;

        [Tooltip("Consulta conferida no modo Query.")]
        [SerializeField] private GameplayTagQuery query = new GameplayTagQuery();

        /// <summary>O que a fonte confere.</summary>
        public TagSourceMode Mode { get => mode; set => mode = value; }

        /// <summary>Tag conferida no modo <see cref="TagSourceMode.Tag"/>.</summary>
        public GameplayTag Tag { get => tag; set => tag = value; }

        /// <summary>Consulta conferida no modo <see cref="TagSourceMode.Query"/>; edite pelas listas dela.</summary>
        public GameplayTagQuery Query => query;

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<TagIndicatorModel> CreateBinding() => new Binding(this);

        /// <summary>Aceso.</summary>
        public override TagIndicatorModel CreatePreview(in UIPreviewContext context) => new TagIndicatorModel(true);

        private bool Evaluate(AbilitySystemComponent asc) =>
            mode == TagSourceMode.Query ? asc.HasMatching(query) : asc.HasTag(tag);

        // Assina o OnTagChanged do ASC com um delegate criado uma vez; ligar, desligar e publicar não alocam
        private sealed class Binding : UIBinding<TagIndicatorModel>
        {
            private readonly TagSource _source;
            private readonly Action<GameplayTag, bool> _onTagChanged;

            private AbilitySystemComponent _asc;
            private bool _active;

            public Binding(TagSource source)
            {
                _source = source;
                _onTagChanged = (_, _) => Refresh(false);
            }

            // Dono que não é ASC: estado vazio, sem assinar nada
            protected override void OnAttach(Object owner)
            {
                if (owner is not AbilitySystemComponent asc)
                {
                    Publish(default);
                    return;
                }

                _asc = asc;
                _asc.OnTagChanged += _onTagChanged;
                Refresh(true);
            }

            protected override void OnDetach()
            {
                if (_asc != null) _asc.OnTagChanged -= _onTagChanged;
                _asc = null;
            }

            private void Refresh(bool force)
            {
                bool active = _source.Evaluate(_asc);
                if (!force && active == _active) return;

                _active = active;
                Publish(new TagIndicatorModel(active));
            }
        }
    }
}
