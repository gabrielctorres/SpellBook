using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Thinkers
{
    /// <summary>Onde a <see cref="CreateThinkerAction"/> põe o thinker.</summary>
    public enum ThinkerPosition
    {
        /// <summary>O ponto do evento; sem ponto, a posição do dono da execução.</summary>
        Point,
        /// <summary>A posição do dono da execução.</summary>
        Caster,
        /// <summary>O primeiro alvo vivo da ação, do seletor dela quando há. Sem alvo, não cria.</summary>
        FirstTarget,
    }

    /// <summary>Rotação inicial do thinker criado pela <see cref="CreateThinkerAction"/>.</summary>
    public enum ThinkerRotation
    {
        Identity,
        /// <summary>A rotação do dono da execução.</summary>
        Caster,
        /// <summary>Virado para o ponto do evento, a partir do dono.</summary>
        TowardPoint,
    }

    /// <summary>
    /// Cria um <see cref="GameplayThinker"/> pelo spawner e o liga ao contexto (R8; ADR 0036): o dono da execução é o
    /// criador e o instigador, e habilidade, spec, nível e <c>EffectContext</c> passam ao thinker. O ASC do thinker recebe a
    /// fonte de tempo do criador. Com <see cref="GameplayThinker.EndWithExecution"/>, uma vigia
    /// <see cref="ExecutionLink.FollowsOwner"/> termina o thinker quando o dono da execução termina ou é cancelado. Não
    /// espera: devolve o thinker em <see cref="ActionContext.LastCreatedThinker"/> para as ações seguintes.
    /// </summary>
    [HelpURL(DocUrls.Thinkers + "#createthinkeraction")]
    [Serializable, TypeCategory("Mundo", Summary = "Cria um thinker no mundo, ligado a quem o criou.", Line = "Criar {prefab}")]
    public sealed class CreateThinkerAction : GameplayAction
    {
        [SerializeField, Tooltip("Modelo do thinker criado.")]
        private GameplayThinker prefab;

        [SerializeField, Tooltip("Cria e devolve o thinker. Vazio usa o spawner padrão, por Instantiate.")]
        private SpawnerAsset spawner;

        [SerializeField, Tooltip("Onde o thinker nasce.")]
        private ThinkerPosition position = ThinkerPosition.Point;

        [SerializeField, Tooltip("Rotação inicial do thinker.")]
        private ThinkerRotation rotation = ThinkerRotation.Identity;

        public GameplayThinker Prefab { get => prefab; set => prefab = value; }

        public SpawnerAsset Spawner { get => spawner; set => spawner = value; }

        public ThinkerPosition Position { get => position; set => position = value; }

        public ThinkerRotation Rotation { get => rotation; set => rotation = value; }

        [NonSerialized] private bool _warnedSpawner;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            AbilitySystemComponent creator = context.Owner;
            if (prefab == null || creator == null || !TryGetPosition(context, creator, out Vector3 point)) return ActionStatus.Done;

            SpawnerAsset used = SpawnerAsset.OrDefault(spawner);
            GameObject instance = used.Spawn(prefab.gameObject, point, GetRotation(context, creator, point));
            if (instance == null)
            {
                // Spawner sem instância (pool vazio que não cresce): pula a criação, com aviso uma vez (C17)
                if (!_warnedSpawner)
                {
                    _warnedSpawner = true;
                    Debug.LogWarning($"[{nameof(CreateThinkerAction)}] O spawner '{used.name}' não criou uma instância de '{prefab.name}'; a criação foi pulada.", used);
                }
                return ActionStatus.Done;
            }

            if (!instance.TryGetComponent(out GameplayThinker thinker))
            {
                used.Release(instance);
                return ActionStatus.Done;
            }
            thinker.AbilitySystem.SetTimeSource(creator.TimeSource);
            thinker.Begin(creator, context, used);
            context.LastCreatedThinker = thinker;
            if (thinker.EndWithExecution && !thinker.HasEnded &&
                context.RunFollowingOwner(ThinkerWatchAction.List, out ActionExecutionHandle watch))
            {
                thinker.SetWatch(creator, watch);
            }
            return ActionStatus.Done;
        }

        /// <summary>
        /// Acusa a ação sem prefab (erro). Numa habilidade, acusa também cada valor do thinker em modo Table cujo nome não
        /// está na tabela dela (erro) e o End With Execution numa habilidade Instant, que termina o thinker com a ativação
        /// (aviso).
        /// </summary>
        public override void Validate(ValidationResult result)
        {
            if (prefab == null)
            {
                result.AddError("Create Thinker sem prefab: escolha o thinker criado.", nameof(prefab));
                return;
            }

            // A ação não conhece a habilidade; o objeto validado é ela quando a ação está numa lista da habilidade
            if (!(result.Context is GameplayAbility ability)) return;

            var values = new List<ScalableFloat>();
            prefab.CollectScalables(values);
            for (int i = 0; i < values.Count; i++)
            {
                ScalableFloat value = values[i];
                if (value.Mode != ScalableFloat.ScalableMode.Table || string.IsNullOrEmpty(value.TableName)) continue;
                if (ability.Values != null && ability.Values.IndexOf(value.TableName) >= 0) continue;
                result.AddError($"O thinker \"{prefab.name}\" lê o valor \"{value.TableName}\" da tabela, que não existe nesta habilidade.",
                    nameof(prefab));
            }

            if (prefab.EndWithExecution && ability.Behavior == AbilityBehavior.Instant)
            {
                result.AddWarning("End With Execution numa habilidade Instant: o thinker termina quando a ativação termina. " +
                    "Para a zona que fica depois do lançamento, desligue End With Execution no thinker.", nameof(prefab));
            }
        }

        private bool TryGetPosition(ActionContext context, AbilitySystemComponent creator, out Vector3 point)
        {
            point = creator.transform.position;
            switch (position)
            {
                case ThinkerPosition.Point:
                    if (context.HasPoint) point = context.Point;
                    return true;
                case ThinkerPosition.FirstTarget:
                    List<AbilitySystemComponent> targets = context.Targets;
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (targets[i] == null) continue;
                        point = targets[i].transform.position;
                        return true;
                    }
                    return false;
                default:
                    return true;
            }
        }

        private Quaternion GetRotation(ActionContext context, AbilitySystemComponent creator, Vector3 point)
        {
            switch (rotation)
            {
                case ThinkerRotation.Caster:
                    return creator.transform.rotation;
                case ThinkerRotation.TowardPoint:
                    Vector3 direction = (context.HasPoint ? context.Point : point) - creator.transform.position;
                    return direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : Quaternion.identity;
                default:
                    return Quaternion.identity;
            }
        }
    }
}
