using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// "Ao terminar" do <see cref="Projectile"/> com o motivo no contexto (B-014, TK3; CA19, C12 e C13). O choque é
    /// simulado pelo <c>HandleCollision</c>.
    /// </summary>
    public class ProjectileEndTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private SpawnFactory _spawn;
        private ManualTimeSource _clock;
        private RecordingSpawnerAsset _spawner;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _spawn = new SpawnFactory();
            _clock = _time.CreateManual();
            _spawner = _spawn.Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
            _spawn.Dispose();
        }

        [UnityTest]
        public IEnumerator End_RunsOnce_WithReason_HitLifetimeObstacleReleased()
        {
            var ended = new List<ActionEndReason>();
            CollidingProjectile hit = CreateArmed(ended, null);
            CollidingProjectile obstacle = CreateArmed(ended, null);
            CollidingProjectile released = CreateArmed(ended, null);
            CollidingProjectile expired = CreateArmed(ended, p => p.Lifetime = 1f);

            hit.Collide(_gas.CreateAsc().gameObject);
            hit.Collide(_gas.CreateAsc().gameObject);
            obstacle.Collide(_spawn.Track(new GameObject("Wall_Test")));
            released.ReturnToPool();
            released.ReturnToPool();
            _clock.Advance(1.2f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(ended, Is.EqualTo(new[]
            {
                ActionEndReason.Hit, ActionEndReason.Obstacle, ActionEndReason.Released, ActionEndReason.Lifetime,
            }), "\"Ao terminar\" não rodou uma vez por projétil com o motivo certo");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(4), "cada projétil não voltou ao pool uma vez");
        }

        [UnityTest]
        public IEnumerator End_WithoutActions_SameAsToday()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            var impacts = new List<ActionEndReason>();
            CollidingProjectile hit = _gas.CreateProjectile<CollidingProjectile>(_clock);
            CollidingProjectile expired = _gas.CreateProjectile<CollidingProjectile>(_clock, p => p.Lifetime = 1f);
            hit.Setup(_spawner, GasFactory.CreateActionContext(caster, Vector3.zero),
                new List<GameplayAction> { new RecordEndReasonAction(impacts) });
            expired.Setup(_spawner, GasFactory.CreateActionContext(caster, Vector3.zero), new List<GameplayAction>());

            hit.Collide(_gas.CreateAsc().gameObject);
            _clock.Advance(1.2f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(hit.OnEnded, Is.Empty);
            Assert.That(impacts, Has.Count.EqualTo(1), "o impacto não rodou uma vez");
            Assert.That(_spawner.ReleaseCount, Is.EqualTo(2), "sem \"Ao terminar\", o projétil não voltou ao pool como hoje");
            Assert.That(caster.HasRunningActions(null), Is.False, "o término sem ações deixou execução no ASC de quem lançou");
        }

        [Test]
        public void End_RunsOnCasterAsc_AtCurrentPosition()
        {
            CollidingProjectile projectile = _gas.CreateProjectile<CollidingProjectile>(_clock);
            var seen = new List<ActionContextSnapshot>();
            projectile.OnEnded.Add(new SnapshotAction(seen));
            GameplayAbility ability = _gas.CreateAbility(new SetupProjectileAction(projectile, _spawner));
            AbilitySystemComponent caster = _gas.CreateAsc();
            SetMaxLevel(ability, 3);
            caster.GiveAbility(ability, 3);
            caster.TryActivateAbility(ability, Vector3.zero);
            projectile.transform.position = new Vector3(3f, 0f, 1f);

            projectile.ReturnToPool();

            Assert.That(seen, Has.Count.EqualTo(1), "\"Ao terminar\" não rodou uma vez");
            ActionContextSnapshot end = seen[0];
            Assert.That(end.Owner, Is.SameAs(caster), "\"Ao terminar\" não rodou no ASC de quem lançou");
            Assert.That(end.Instigator, Is.SameAs(caster));
            Assert.That(end.Ability, Is.SameAs(ability), "\"Ao terminar\" perdeu a habilidade de quem lançou");
            Assert.That(end.Level, Is.EqualTo(3), "\"Ao terminar\" perdeu o nível da habilidade");
            Assert.That(end.HasPoint, Is.True);
            Assert.That(end.Point, Is.EqualTo(new Vector3(3f, 0f, 1f)), "o ponto não é a posição do projétil no término");
            Assert.That(end.TargetCount, Is.EqualTo(0), "\"Ao terminar\" recebeu alvos do evento");
        }

        [Test]
        public void EndReason_IsNone_InImpact()
        {
            var impacts = new List<ActionEndReason>();
            var ended = new List<ActionEndReason>();
            CollidingProjectile projectile = _gas.CreateProjectile<CollidingProjectile>(_clock);
            projectile.OnEnded.Add(new RecordEndReasonAction(ended));
            projectile.Setup(_spawner, GasFactory.CreateActionContext(_gas.CreateAsc(), Vector3.zero),
                new List<GameplayAction> { new RecordEndReasonAction(impacts) });

            projectile.Collide(_gas.CreateAsc().gameObject);

            Assert.That(impacts, Is.EqualTo(new[] { ActionEndReason.None }), "\"No impacto\" recebeu um motivo de término");
            Assert.That(ended, Is.EqualTo(new[] { ActionEndReason.Hit }));
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var so = new UnityEditor.SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Projétil com <c>Setup</c> feito e "Ao terminar" que registra o motivo em <paramref name="ended"/>.</summary>
        private CollidingProjectile CreateArmed(List<ActionEndReason> ended, Action<CollidingProjectile> configure)
        {
            CollidingProjectile projectile = _gas.CreateProjectile(_clock, configure);
            projectile.OnEnded.Add(new RecordEndReasonAction(ended));
            projectile.Setup(_spawner, GasFactory.CreateActionContext(_gas.CreateAsc(), Vector3.zero), new List<GameplayAction>());
            return projectile;
        }

        /// <summary>Projétil que expõe o <c>HandleCollision</c> para simular um choque sem física.</summary>
        private sealed class CollidingProjectile : Projectile
        {
            public void Collide(GameObject other) => HandleCollision(other);
        }

        private sealed class RecordEndReasonAction : GameplayAction
        {
            private readonly List<ActionEndReason> _into;

            public RecordEndReasonAction(List<ActionEndReason> into) => _into = into;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _into.Add(context.EndReason);
                return ActionStatus.Done;
            }
        }

        /// <summary>Cópia do que importa do contexto: o executor devolve o contexto ao pool quando a lista acaba.</summary>
        private struct ActionContextSnapshot
        {
            public AbilitySystemComponent Owner;
            public AbilitySystemComponent Instigator;
            public GameplayAbility Ability;
            public int Level;
            public bool HasPoint;
            public Vector3 Point;
            public int TargetCount;
        }

        private sealed class SnapshotAction : GameplayAction
        {
            private readonly List<ActionContextSnapshot> _into;

            public SnapshotAction(List<ActionContextSnapshot> into) => _into = into;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _into.Add(new ActionContextSnapshot
                {
                    Owner = context.Owner,
                    Instigator = context.Instigator,
                    Ability = context.Ability,
                    Level = context.Level,
                    HasPoint = context.HasPoint,
                    Point = context.Point,
                    TargetCount = context.EventTargets.Count,
                });
                return ActionStatus.Done;
            }
        }

        /// <summary>Faz o <c>Setup</c> do projétil com o contexto da ativação e sem impacto, como a <c>SpawnProjectileAction</c>.</summary>
        private sealed class SetupProjectileAction : GameplayAction
        {
            private readonly Projectile _projectile;
            private readonly RecordingSpawnerAsset _spawner;

            public SetupProjectileAction(Projectile projectile, RecordingSpawnerAsset spawner)
            {
                _projectile = projectile;
                _spawner = spawner;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _projectile.Setup(_spawner, context, new List<GameplayAction>());
                return ActionStatus.Done;
            }
        }
    }
}
