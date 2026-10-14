using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;
using SpellBook.GAS.Tags;

namespace SpellBook.GAS.Targeting
{
    public enum PhysicsMode { Physics3D, Physics2D }

    public abstract class TargetingStrategy : ScriptableObject
    {
        [SerializeField] protected PhysicsMode physicsMode = PhysicsMode.Physics3D;

        [Header("Filters")]
        [Tooltip("Permite que o próprio conjurador seja alvo.")]
        [SerializeField] protected bool includeSelf = false;
        [Tooltip("O alvo precisa ter todas estas tags.")]
        [SerializeField] protected List<GameplayTag> requiredTargetTags = new List<GameplayTag>();
        [Tooltip("Alvos com alguma destas tags são ignorados (ex: tag do próprio time).")]
        [SerializeField] protected List<GameplayTag> ignoredTargetTags = new List<GameplayTag>();

        private const int BufferSize = 64;
        private static readonly Collider[] _buffer3D = new Collider[BufferSize];
        private static readonly List<Collider2D> _buffer2D = new List<Collider2D>(BufferSize);
        private static readonly HashSet<AbilitySystemComponent> _seen = new HashSet<AbilitySystemComponent>();

        public abstract IEnumerable<AbilitySystemComponent> GetTargets(AbilitySystemComponent source, Vector3 origin);

        protected virtual bool IsValidTarget(AbilitySystemComponent source, AbilitySystemComponent candidate)
        {
            if (candidate == null) return false;
            if (!includeSelf && candidate == source) return false;
            if (!candidate.HasAllTags(requiredTargetTags)) return false;
            if (candidate.HasAnyTag(ignoredTargetTags)) return false;
            return true;
        }

        /// <summary>
        /// Busca ASCs em um raio (2D ou 3D conforme physicsMode), sem duplicatas e já filtrados.
        /// O ASC é procurado no collider ou em seus pais, então unidades com vários colliders contam uma vez.
        /// </summary>
        protected void OverlapCircle(AbilitySystemComponent source, Vector3 origin, float radius, LayerMask layer, List<AbilitySystemComponent> results)
        {
            _seen.Clear();

            if (physicsMode == PhysicsMode.Physics3D)
            {
                int count = Physics.OverlapSphereNonAlloc(origin, radius, _buffer3D, layer);
                for (int i = 0; i < count; i++) TryAdd(source, _buffer3D[i].GetComponentInParent<AbilitySystemComponent>(), results);
            }
            else
            {
                var filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(layer);
                Physics2D.OverlapCircle(origin, radius, filter, _buffer2D);
                for (int i = 0; i < _buffer2D.Count; i++) TryAdd(source, _buffer2D[i].GetComponentInParent<AbilitySystemComponent>(), results);
            }
        }

        /// <summary>
        /// Ordena os alvos pela distância até a origem (mais próximo primeiro).
        /// </summary>
        protected static void SortByDistance(List<AbilitySystemComponent> targets, Vector3 origin)
        {
            targets.Sort((a, b) =>
                (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
        }

        private void TryAdd(AbilitySystemComponent source, AbilitySystemComponent candidate, List<AbilitySystemComponent> results)
        {
            if (candidate == null || !_seen.Add(candidate)) return;
            if (IsValidTarget(source, candidate)) results.Add(candidate);
        }
    }
}
