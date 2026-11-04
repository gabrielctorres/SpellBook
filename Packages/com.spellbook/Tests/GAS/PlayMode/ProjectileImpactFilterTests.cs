using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Filtro de impacto do <see cref="Projectile"/>, o filtro de candidato da ADR 0035 (B-014, TK3; CA20, C14). O
    /// choque é simulado pelo <c>HandleCollision</c>.
    /// </summary>
    public class ProjectileImpactFilterTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private SpawnFactory _spawn;
        private ManualTimeSource _clock;
        private RecordingSpawnerAsset _spawner;
        private List<AbilitySystemComponent> _hits;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _spawn = new SpawnFactory();
            _clock = _time.CreateManual();
            _spawner = _spawn.Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            _hits = new List<AbilitySystemComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
            _spawn.Dispose();
        }

        [Test]
        public void ImpactFilter_Fails_PassesThrough_NoImpactNoPierceSpentNoEnd()
        {
            GameplayTag enemy = _gas.CreateTag("Team.Enemy");
            var filter = new TagQueryCondition { Subject = ConditionSubject.TargetUnit };
            filter.Query.All.Add(enemy);
            AbilitySystemComponent ally = _gas.CreateAsc(), first = _gas.CreateAsc(), second = _gas.CreateAsc();
            first.AddTag(enemy);
            second.AddTag(enemy);
            CollidingProjectile projectile = CreateArmed(p =>
            {
                p.Pierce = 1;
                p.ImpactFilter = filter;
            }, _gas.CreateAsc());

            projectile.Collide(ally.gameObject);
            projectile.Collide(first.gameObject);
            projectile.Collide(ally.gameObject);
            int releasedBeforeSecond = _spawner.ReleaseCount;
            projectile.Collide(second.gameObject);

            Assert.That(_hits, Is.EqualTo(new[] { first, second }), "o candidato que não passa no filtro recebeu impacto");
            Assert.That(releasedBeforeSecond, Is.EqualTo(0), "o candidato filtrado terminou o projétil ou gastou a perfuração");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(1), "o projétil não terminou depois de gastar a perfuração");
        }

        [Test]
        public void ImpactFilter_OwnerIsCaster_WithAbilityLevel()
        {
            var seen = new List<ConditionContext>();
            CollidingProjectile projectile = _gas.CreateProjectile<CollidingProjectile>(_clock,
                p => p.ImpactFilter = new RecordCondition(seen));
            GameplayAbility ability = _gas.CreateAbility(new SetupProjectileAction(projectile, _spawner,
                new List<GameplayAction> { new RecordTargetAction(_hits) }));
            AbilitySystemComponent caster = _gas.CreateAsc(), target = _gas.CreateAsc();
            SetMaxLevel(ability, 2);
            caster.GiveAbility(ability, 2);
            caster.TryActivateAbility(ability, Vector3.zero);

            projectile.Collide(target.gameObject);

            Assert.That(seen, Has.Count.EqualTo(1), "o filtro não foi avaliado uma vez no choque");
            Assert.That(seen[0].Owner, Is.SameAs(caster), "o Owner do filtro não é quem lançou");
            Assert.That(seen[0].Target.Unit, Is.SameAs(target), "o Target do filtro não é o ASC atingido");
            Assert.That(seen[0].Ability, Is.SameAs(ability));
            Assert.That(seen[0].Level, Is.EqualTo(2));
            Assert.That(_hits, Is.EqualTo(new[] { target }), "o candidato que passou não recebeu impacto");
        }

        [Test]
        public void ImpactFilter_Passing_OwnerStillIgnoredWithoutDamageOwner()
        {
            var seen = new List<ConditionContext>();
            AbilitySystemComponent caster = _gas.CreateAsc();
            CollidingProjectile projectile = CreateArmed(p => p.ImpactFilter = new RecordCondition(seen), caster);

            projectile.Collide(caster.gameObject);

            Assert.That(_hits, Is.Empty, "o dono foi acertado sem DamageOwner");
            Assert.That(seen, Is.Empty, "o filtro foi avaliado para o dono ignorado");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(0));
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var so = new UnityEditor.SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Projétil com <c>Setup</c> feito, que registra em <c>_hits</c> o alvo de cada impacto.</summary>
        private CollidingProjectile CreateArmed(System.Action<CollidingProjectile> configure, AbilitySystemComponent caster)
        {
            CollidingProjectile projectile = _gas.CreateProjectile(_clock, configure);
            projectile.Setup(_spawner, caster, new List<GameplayAction> { new RecordTargetAction(_hits) });
            return projectile;
        }

        /// <summary>Projétil que expõe o <c>HandleCollision</c> para simular um choque sem física.</summary>
        private sealed class CollidingProjectile : Projectile
        {
            public void Collide(GameObject other) => HandleCollision(other);
        }

        /// <summary>Condição verdadeira que registra o contexto de cada avaliação.</summary>
        private sealed class RecordCondition : Condition<ConditionContext>
        {
            private readonly List<ConditionContext> _into;

            public RecordCondition(List<ConditionContext> into) => _into = into;

            public override bool Evaluate(in ConditionContext context)
            {
                _into.Add(context);
                return true;
            }
        }

        private sealed class RecordTargetAction : GameplayAction
        {
            private readonly List<AbilitySystemComponent> _into;

            public RecordTargetAction(List<AbilitySystemComponent> into) => _into = into;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _into.Add(context.EventTargets.Count > 0 ? context.EventTargets[0] : null);
                return ActionStatus.Done;
            }
        }

        /// <summary>Faz o <c>Setup</c> do projétil com o contexto da ativação, como a <c>SpawnProjectileAction</c>.</summary>
        private sealed class SetupProjectileAction : GameplayAction
        {
            private readonly Projectile _projectile;
            private readonly RecordingSpawnerAsset _spawner;
            private readonly List<GameplayAction> _onImpact;

            public SetupProjectileAction(Projectile projectile, RecordingSpawnerAsset spawner, List<GameplayAction> onImpact)
            {
                _projectile = projectile;
                _spawner = spawner;
                _onImpact = onImpact;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _projectile.Setup(_spawner, context, _onImpact);
                return ActionStatus.Done;
            }
        }
    }
}
