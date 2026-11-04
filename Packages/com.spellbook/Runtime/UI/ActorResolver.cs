using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI
{
    /// <summary>Como o <see cref="ActorResolver"/> acha o dono da UI (ADR 0017, item 2).</summary>
    public enum ActorResolveMode
    {
        /// <summary>No próprio objeto ou num pai.</summary>
        SelfOrParent,

        /// <summary>No valor de uma <see cref="RuntimeAnchor"/>, publicado pelo ator ao nascer.</summary>
        FromAnchor,

        /// <summary>Na referência dada no Inspector.</summary>
        Explicit,
    }

    /// <summary>
    /// Acha o dono de uma UI sem busca global (ADR 0039, item 5): no objeto ou num pai, numa âncora ou por referência.
    /// Dono não encontrado não lança exceção.
    /// </summary>
    [Serializable]
    public sealed class ActorResolver
    {
        [SerializeField]
        [Tooltip("Onde procurar o dono.")]
        private ActorResolveMode mode = ActorResolveMode.SelfOrParent;

        [SerializeField]
        [Tooltip("Âncora que guarda o dono, no modo FromAnchor.")]
        [ShowIf(nameof(mode), ActorResolveMode.FromAnchor)]
        private RuntimeAnchor anchor;

        [SerializeField]
        [Tooltip("O dono, ou um GameObject que o tenha, no modo Explicit.")]
        [ShowIf(nameof(mode), ActorResolveMode.Explicit)]
        private Object explicitOwner;

        /// <summary>Onde procurar o dono.</summary>
        public ActorResolveMode Mode => mode;

        /// <summary>Âncora do modo <see cref="ActorResolveMode.FromAnchor"/>.</summary>
        public RuntimeAnchor Anchor => anchor;

        /// <summary>Referência do modo <see cref="ActorResolveMode.Explicit"/>.</summary>
        public Object ExplicitOwner => explicitOwner;

        /// <summary>Resolvedor que procura no próprio objeto ou num pai.</summary>
        public static ActorResolver ForSelfOrParent()
        {
            return new ActorResolver { mode = ActorResolveMode.SelfOrParent };
        }

        /// <summary>Resolvedor que lê o dono de <paramref name="runtimeAnchor"/>.</summary>
        public static ActorResolver ForAnchor(RuntimeAnchor runtimeAnchor)
        {
            return new ActorResolver { mode = ActorResolveMode.FromAnchor, anchor = runtimeAnchor };
        }

        /// <summary>Resolvedor que usa <paramref name="owner"/>, ou o componente do tipo procurado nele se for um <c>GameObject</c>.</summary>
        public static ActorResolver ForExplicit(Object owner)
        {
            return new ActorResolver { mode = ActorResolveMode.Explicit, explicitOwner = owner };
        }

        /// <summary>
        /// Procura um dono do tipo <paramref name="ownerType"/> a partir de <paramref name="from"/>. Devolve <c>false</c>,
        /// sem exceção, se não houver dono, se ele for de outro tipo ou se um argumento for nulo.
        /// </summary>
        public bool TryResolve(Component from, Type ownerType, out Object owner)
        {
            owner = null;
            if (from == null || ownerType == null)
            {
                return false;
            }

            bool component = typeof(Component).IsAssignableFrom(ownerType) || ownerType.IsInterface;
            switch (mode)
            {
                case ActorResolveMode.SelfOrParent:
                    owner = component ? from.GetComponentInParent(ownerType) : null;
                    break;
                case ActorResolveMode.FromAnchor:
                    owner = anchor != null ? OfType(anchor.UntypedValue, ownerType) : null;
                    break;
                case ActorResolveMode.Explicit:
                    owner = OfType(explicitOwner, ownerType);
                    if (owner == null && component && explicitOwner is GameObject gameObject && gameObject != null)
                    {
                        owner = gameObject.GetComponent(ownerType);
                    }

                    break;
            }

            // Objeto destruído compara igual a null, mas não é null: devolve o null de verdade
            if (owner == null)
            {
                owner = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Acrescenta ao <paramref name="result"/> os problemas do resolvedor, nos campos sob <paramref name="propertyPath"/>:
        /// modo sem âncora ou sem referência, âncora de outro tipo e, só num objeto em cena, nenhum dono do tipo no objeto
        /// nem nos pais (aviso). Sem <paramref name="ownerType"/> (binder sem fonte), confere só os campos do modo.
        /// </summary>
        public void Validate(ValidationResult result, Component from, Type ownerType, string propertyPath)
        {
            switch (mode)
            {
                case ActorResolveMode.FromAnchor when anchor == null:
                    result.AddError("Modo FromAnchor sem âncora. Escolha a âncora que guarda o dono.", propertyPath + ".anchor");
                    break;
                case ActorResolveMode.FromAnchor when ownerType != null && !ownerType.IsAssignableFrom(anchor.ValueType):
                    result.AddError($"A âncora guarda {anchor.ValueType.Name}, mas a fonte lê {ownerType.Name}. " +
                        "Escolha uma âncora do tipo da fonte.", propertyPath + ".anchor");
                    break;
                case ActorResolveMode.Explicit when explicitOwner == null:
                    result.AddError("Modo Explicit sem referência. Ligue o dono.", propertyPath + ".explicitOwner");
                    break;
                case ActorResolveMode.SelfOrParent when ownerType != null && from != null && IsPlacedInScene(from.gameObject)
                    && (typeof(Component).IsAssignableFrom(ownerType) || ownerType.IsInterface)
                    && from.GetComponentInParent(ownerType, true) == null:
                    result.AddWarning($"Sem {ownerType.Name} neste objeto nem nos pais. A view mostra o estado vazio.", propertyPath);
                    break;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Diz se o objeto está numa cena de prévia do Editor, como a do Prefab Mode. Preenchido pelo código de Editor,
        /// que o runtime não referencia.
        /// </summary>
        internal static Func<GameObject, bool> IsInPreviewScene;
#endif

        // Prefab em asset não tem cena, e o prefab aberto no Prefab Mode fica numa cena de prévia: os dois ficam fora
        private static bool IsPlacedInScene(GameObject gameObject)
        {
            if (!gameObject.scene.IsValid())
            {
                return false;
            }
#if UNITY_EDITOR
            if (IsInPreviewScene != null && IsInPreviewScene(gameObject))
            {
                return false;
            }
#endif
            return true;
        }

        private static Object OfType(Object value, Type ownerType)
        {
            return value != null && ownerType.IsInstanceOfType(value) ? value : null;
        }
    }
}
