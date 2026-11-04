using System;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>
    /// Publica um componente numa <see cref="RuntimeAnchor"/> enquanto está ligado (ADR 0039, item 5). Sai da âncora ao
    /// desligar, mas só se ela ainda guarda este componente: no respawn, o novo ator não é apagado pelo antigo.
    /// </summary>
    [HelpURL(DocUrls.Concepts + "#ancoras")]
    public sealed class AnchorRegistrar : MonoBehaviour, IValidatable
    {
        [Tooltip("Âncora em que o componente é publicado.")]
        [SerializeField]
        private RuntimeAnchor anchor;

        [Tooltip("Componente publicado. Vazio: o componente do tipo da âncora neste objeto.")]
        [SerializeField]
        private Component value;

        private Component _resolved;

        private void Awake()
        {
            _resolved = value != null ? value : FindOwnComponent();
        }

        private void OnEnable()
        {
            if (anchor != null && _resolved != null)
            {
                anchor.ProvideUntyped(_resolved);
            }
        }

        private void OnDisable()
        {
            if (anchor != null && _resolved != null)
            {
                anchor.UnsetUntyped(_resolved);
            }
        }

        /// <inheritdoc />
        public void Validate(ValidationResult result)
        {
            if (anchor == null)
            {
                result.AddError("Sem âncora. O componente não é publicado.", nameof(anchor));
                return;
            }

            Type type = anchor.ValueType;
            if (value != null)
            {
                if (!type.IsInstanceOfType(value))
                {
                    result.AddError($"O componente não é {type.Name}, o tipo da âncora.", nameof(value));
                }

                return;
            }

            if (FindOwnComponent() == null)
            {
                result.AddError($"Sem componente e sem {type.Name} neste objeto para publicar.", nameof(value));
            }
        }

        private Component FindOwnComponent()
        {
            if (anchor == null)
            {
                return null;
            }

            Type type = anchor.ValueType;
            if (!type.IsInterface && !typeof(Component).IsAssignableFrom(type))
            {
                return null;
            }

            return GetComponent(type);
        }
    }
}
