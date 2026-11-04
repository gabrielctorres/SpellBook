#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Teams;
using SpellBook.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// <see cref="TeamRelationCondition"/> como filtro de candidato da aura e de impacto do projétil (B-014, TK8; CA14;
    /// ADR 0035). O dono do filtro é quem criou o thinker ou lançou o projétil.
    /// </summary>
    public class AuraTeamFilterTests
    {
        private GasFactory _gas;
        private TeamsFactory _teams;
        private TeamRelationMatrix _matrix;
        private TeamDefinition _red;
        private TeamDefinition _blue;
        private readonly List<GameObject> _units = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _teams = new TeamsFactory();
            _red = _teams.CreateTeam("Red");
            _blue = _teams.CreateTeam("Blue");
            _matrix = _teams.CreateMatrix(TeamRelation.Neutral, _red, _blue);
            _teams.SetCell(_matrix, _red, _blue, TeamRelation.Hostile);
        }

        [TearDown]
        public void TearDown()
        {
            // No PlayMode, o Dispose usa Destroy, que só age no fim do quadro: os colliders seguiriam no próximo teste.
            foreach (GameObject unit in _units)
            {
                if (unit != null) Object.DestroyImmediate(unit);
            }
            _units.Clear();
            Physics.SyncTransforms();
            _gas.Dispose();
            _teams.Dispose();
        }

        [Test]
        public void Aura_TeamFilter_OnlyAllies()
        {
            GameplayTag blessing = _gas.CreateTag("Aura.Bencao");
            var aura = new AuraBehaviour { Effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, blessing) };
            aura.Area.Radius = ScalableFloat.Constant(5f);
            aura.Area.Filter = new TeamRelationCondition { Mask = TeamRelationMask.Allied };
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 1f, t =>
            {
                t.Behaviour = aura;
                t.EndWithExecution = false;
            });
            thinker.gameObject.SetActive(true);
            AbilitySystemComponent caster = Unit(_red, new Vector3(50f, 0f, 0f));
            AbilitySystemComponent ally = Unit(_red, new Vector3(1f, 0f, 0f));
            AbilitySystemComponent enemy = Unit(_blue, new Vector3(-1f, 0f, 0f));

            thinker.Begin(caster, null, null);

            Assert.That(ally.HasTag(blessing), Is.True, "o aliado de quem criou a aura não a recebeu");
            Assert.That(enemy.HasTag(blessing), Is.False, "o inimigo de quem criou a aura a recebeu");
        }

        [Test]
        public void Projectile_TeamFilter_OnlyHostiles()
        {
            AbilitySystemComponent caster = Unit(_red, Vector3.zero);
            AbilitySystemComponent ally = Unit(_red, Vector3.zero);
            AbilitySystemComponent enemy = Unit(_blue, Vector3.zero);
            var hits = new List<AbilitySystemComponent>();
            CollidingProjectile projectile = _gas.CreateProjectile<CollidingProjectile>(null, p =>
            {
                p.UnlimitedPierce = true;
                p.ImpactFilter = new TeamRelationCondition { Mask = TeamRelationMask.Hostile };
            });
            projectile.Setup(null, caster, new List<GameplayAction> { new RecordTargetAction(hits) });

            projectile.Collide(ally.gameObject);
            projectile.Collide(enemy.gameObject);

            Assert.That(hits, Is.EqualTo(new[] { enemy }), "o filtro deixou passar o aliado ou barrou o inimigo");
        }

        private AbilitySystemComponent Unit(TeamDefinition team, Vector3 position)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.transform.position = position;
            asc.gameObject.AddComponent<SphereCollider>().radius = 0.25f;
            _teams.AddMember(asc.gameObject, team, _matrix);
            _units.Add(asc.gameObject);
            Physics.SyncTransforms();
            return asc;
        }

        /// <summary>Projétil que expõe o <c>HandleCollision</c> para simular um choque sem física.</summary>
        private sealed class CollidingProjectile : Projectile
        {
            public void Collide(GameObject other) => HandleCollision(other);
        }

        /// <summary>Registra o alvo de cada impacto.</summary>
        private sealed class RecordTargetAction : GameplayAction
        {
            private readonly List<AbilitySystemComponent> _into;

            public RecordTargetAction(List<AbilitySystemComponent> into) => _into = into;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _into.AddRange(context.Targets);
                return ActionStatus.Done;
            }
        }
    }
}
#endif
