#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Teams;
using SpellBook.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary><see cref="TeamRelationCondition"/> como filtro de candidato do <see cref="AreaSelector"/> (Teams, TM5; CA13).</summary>
    public class TeamAreaFilterTests
    {
        private static readonly object Owner = new object();

        private GasFactory _gas;
        private TeamsFactory _teams;
        private TeamRelationMatrix _matrix;
        private readonly List<GameObject> _units = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _teams = new TeamsFactory();
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
        public void AreaFilter_OnlyHostiles_AlliesPlusSelf()
        {
            TeamDefinition red = _teams.CreateTeam("Red");
            TeamDefinition blue = _teams.CreateTeam("Blue");
            TeamDefinition green = _teams.CreateTeam("Green");
            _matrix = _teams.CreateMatrix(TeamRelation.Neutral, red, blue, green);
            _teams.SetCell(_matrix, red, blue, TeamRelation.Hostile);
            AbilitySystemComponent caster = Unit(red, Vector3.zero);
            AbilitySystemComponent enemy = Unit(blue, new Vector3(1f, 0f, 0f));
            AbilitySystemComponent ally = Unit(red, new Vector3(2f, 0f, 0f));
            AbilitySystemComponent neutral = Unit(green, new Vector3(3f, 0f, 0f));
            AbilitySystemComponent farEnemy = Unit(blue, new Vector3(4f, 0f, 0f));
            var filter = new TeamRelationCondition { Mask = TeamRelationMask.Hostile };
            var area = new AreaSelector { Radius = ScalableFloat.Constant(5f), IncludeOwner = true, Filter = filter };

            List<AbilitySystemComponent> hostiles = Select(caster, area);
            filter.Mask = TeamRelationMask.Allied | TeamRelationMask.Self;
            List<AbilitySystemComponent> alliesAndSelf = Select(caster, area);
            area.IncludeOwner = false;
            List<AbilitySystemComponent> alliesWithoutOwner = Select(caster, area);
            filter.Mask = TeamRelationMask.Neutral;
            List<AbilitySystemComponent> neutrals = Select(caster, area);

            Assert.That(hostiles, Is.EqualTo(new[] { enemy, farEnemy }), "só inimigos, sem o dono incluído");
            Assert.That(alliesAndSelf, Is.EqualTo(new[] { caster, ally }), "aliados e o próprio dono");
            Assert.That(alliesWithoutOwner, Is.EqualTo(new[] { ally }), "sem o dono, só o aliado");
            Assert.That(neutrals, Is.EqualTo(new[] { neutral }), "só o neutro");
        }

        private static List<AbilitySystemComponent> Select(AbilitySystemComponent caster, TargetSelector selector)
        {
            var record = new RecordTargetsAction { Target = selector };
            caster.RunActions(new GameplayAction[] { record }, GasFactory.CreateActionContext(caster, Vector3.zero), Owner);
            return record.Seen;
        }

        private AbilitySystemComponent Unit(TeamDefinition team, Vector3 position)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.transform.position = position;
            asc.gameObject.AddComponent<SphereCollider>().radius = 0.25f;
            _teams.AddMember(asc.gameObject, team, _matrix);
            _units.Add(asc.gameObject);
            Physics.SyncTransforms();
            return asc;
        }

        /// <summary>Copia os alvos resolvidos da ação para <see cref="Seen"/>.</summary>
        [Serializable]
        private sealed class RecordTargetsAction : GameplayAction
        {
            public readonly List<AbilitySystemComponent> Seen = new List<AbilitySystemComponent>();

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Seen.AddRange(context.Targets);
                return ActionStatus.Done;
            }
        }
    }
}
#endif
