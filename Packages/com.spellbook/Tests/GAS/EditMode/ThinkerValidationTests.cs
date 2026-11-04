using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Validação do thinker, da aura, do projétil e da CreateThinkerAction (B-014: CA30, R23).</summary>
    public class ThinkerValidationTests
    {
        private const string Action0 = "OnActivate.Array.data[0]";
        private const string TempPrefab = "Assets/ThinkerValidationTests_Temp.prefab";

        private GasFactory _gas;
        private readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _created.Clear();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) StageUtility.GoToMainStage();
            AssetDatabase.DeleteAsset(TempPrefab);
            _gas.Dispose();
        }

        // ----- Thinker -----

        [Test]
        public void Thinker_Valid_NoMessages()
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(5f, 1f);

            Assert.That(Format(Validate(thinker)), Is.Empty);
        }

        [Test]
        public void Thinker_NeverEnds_Warns()
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 0f, t => t.EndWithExecution = false);

            AssertSingle(thinker, ValidationSeverity.Warning, "duration");
        }

        [TestCase(-1f, 1f, "duration")]
        [TestCase(5f, -1f, "pulseInterval")]
        public void Thinker_NegativeConstant_Error(float duration, float interval, string path)
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(duration, interval);

            AssertSingle(thinker, ValidationSeverity.Error, path);
        }

        [Test]
        public void Thinker_PulseActionsWithoutInterval_Warns()
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(5f, 0f, t => t.OnPulse.Add(new WaitAction()));

            AssertSingle(thinker, ValidationSeverity.Warning, "onPulse");
        }

        [Test]
        public void Thinker_Scene_InstigatorReader_Warns()
        {
            GameplayThinker thinker = CreateSceneThinker();
            thinker.OnPulse.Add(new ChanceAction { Actions = new List<GameplayAction> { new ApplyEffectAction() } });

            ValidationMessage message = AssertSingle(thinker, ValidationSeverity.Warning, "onPulse.Array.data[0].actions.Array.data[0]");
            StringAssert.Contains("instigador", message.Message);
        }

        [Test]
        public void Thinker_PrefabAsset_InstigatorReader_NoWarning()
        {
            GameplayThinker model = _gas.CreateThinkerPrefab(5f, 1f, t => t.OnCreated.Add(new AnimationAction()));
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(model.gameObject, TempPrefab);

            Assert.That(Format(Validate(asset.GetComponent<GameplayThinker>())), Is.Empty);
        }

        [Test]
        public void Thinker_PrefabStage_InstigatorReader_NoWarning()
        {
            GameplayThinker model = _gas.CreateThinkerPrefab(5f, 1f, t => t.OnCreated.Add(new ApplyEffectAction()));
            PrefabUtility.SaveAsPrefabAsset(model.gameObject, TempPrefab);

            PrefabStage stage = PrefabStageUtility.OpenPrefab(TempPrefab);
            GameplayThinker opened = stage.prefabContentsRoot.GetComponent<GameplayThinker>();

            Assert.That(opened.gameObject.scene.IsValid(), Is.True, "a cena de prévia do Prefab Mode é válida");
            Assert.That(Format(Validate(opened)), Is.Empty);
        }

        // ----- Aura -----

        [Test]
        public void Aura_NoEffect_Error()
        {
            GameplayThinker thinker = CreateAuraThinker(1f, aura => aura.Effect = null);

            AssertSingle(thinker, ValidationSeverity.Error, "behaviour.effect");
        }

        [Test]
        public void Aura_InstantEffect_Warns()
        {
            GameplayThinker thinker = CreateAuraThinker(1f, aura => aura.Effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f));

            AssertSingle(thinker, ValidationSeverity.Warning, "behaviour.effect");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Aura_RadiusNotPositive_Error(float radius)
        {
            GameplayThinker thinker = CreateAuraThinker(1f, aura => aura.Area.Radius = ScalableFloat.Constant(radius));

            AssertSingle(thinker, ValidationSeverity.Error, "behaviour.area.radius");
        }

        [Test]
        public void Aura_NoInterval_WarnsOnlyInitialPulse()
        {
            GameplayThinker thinker = CreateAuraThinker(0f);

            ValidationMessage message = AssertSingle(thinker, ValidationSeverity.Warning, "pulseInterval");
            StringAssert.Contains("pulso inicial", message.Message);
        }

        [Test]
        public void Aura_IntervalBelowOneFrame_Warns()
        {
            GameplayThinker thinker = CreateAuraThinker(0.01f);

            AssertSingle(thinker, ValidationSeverity.Warning, "pulseInterval");
        }

        // ----- Projétil -----

        [Test]
        public void Projectile_Defaults_NoMessages()
        {
            Projectile projectile = CreateProjectile();

            Assert.That(Format(Validate(projectile)), Is.Empty);
        }

        [Test]
        public void Projectile_NegativePierce_Error()
        {
            Projectile projectile = CreateProjectile();
            projectile.Pierce = -1;

            AssertSingle(projectile, ValidationSeverity.Error, nameof(Projectile.Pierce));
        }

        [Test]
        public void Projectile_PierceAbove31_Warns()
        {
            Projectile projectile = CreateProjectile();
            projectile.Pierce = 32;

            AssertSingle(projectile, ValidationSeverity.Warning, nameof(Projectile.Pierce));
        }

        [Test]
        public void Projectile_UnlimitedWithoutLifetime_Warns()
        {
            Projectile projectile = CreateProjectile();
            projectile.UnlimitedPierce = true;
            projectile.Lifetime = 0f;

            AssertSingle(projectile, ValidationSeverity.Warning, nameof(Projectile.UnlimitedPierce));
        }

        [Test]
        public void Projectile_HitEachTargetOnceOff_WithPierce_Warns()
        {
            Projectile projectile = CreateProjectile();
            projectile.Pierce = 2;
            projectile.HitEachTargetOnce = false;

            AssertSingle(projectile, ValidationSeverity.Warning, nameof(Projectile.HitEachTargetOnce));
        }

        [Test]
        public void Projectile_OnEndedSpawnsSamePrefab_WarnsLoop()
        {
            Projectile projectile = CreateProjectile();
            projectile.OnEnded.Add(new SpawnProjectileAction { ProjectilePrefab = projectile.gameObject });

            ValidationMessage message = AssertSingle(projectile, ValidationSeverity.Warning, "OnEnded.Array.data[0]");
            StringAssert.Contains("laço", message.Message);
        }

        // ----- CreateThinkerAction -----

        [Test]
        public void CreateThinker_NoPrefab_Error()
        {
            GameplayAbility ability = _gas.CreateAbility(new CreateThinkerAction());

            AssertSingle(ability, ValidationSeverity.Error, Action0 + ".prefab");
        }

        [Test]
        public void CreateThinker_TableNameMissing_Error()
        {
            GameplayAbility ability = _gas.CreateAbility(new CreateThinkerAction { Prefab = CreateTableThinker() });

            ValidationResult result = Validate(ability);

            Assert.That(result.Messages.Select(m => $"{m.Severity} {m.PropertyPath}"),
                Is.EqualTo(new[] { "Error " + Action0 + ".prefab", "Error " + Action0 + ".prefab" }));
            StringAssert.Contains("\"vida\"", result.Messages[0].Message);
            StringAssert.Contains("\"raio\"", result.Messages[1].Message);
        }

        [Test]
        public void CreateThinker_TableNameInAbility_NoMessages()
        {
            GameplayAbility ability = _gas.CreateAbility(new CreateThinkerAction { Prefab = CreateTableThinker() });
            ability.Values.Add("vida", ScalableFloat.Constant(3f));
            ability.Values.Add("raio", ScalableFloat.Constant(2f));

            Assert.That(Format(Validate(ability)), Is.Empty);
        }

        [Test]
        public void CreateThinker_InInstantAbility_WithEndWithExecution_Warns()
        {
            GameplayThinker prefab = _gas.CreateThinkerPrefab(5f, 1f);
            GameplayAbility ability = _gas.CreateAbility(new CreateThinkerAction { Prefab = prefab });

            ValidationMessage message = AssertSingle(ability, ValidationSeverity.Warning, Action0 + ".prefab");
            StringAssert.Contains("Instant", message.Message);
        }

        // ----- Auxiliares -----

        // Thinker ativo na cena aberta, como o designer o coloca: sem criador e sem instigador (P2)
        private GameplayThinker CreateSceneThinker()
        {
            var go = new GameObject("Thinker_Scene");
            _created.Add(go);
            var thinker = go.AddComponent<GameplayThinker>();
            thinker.Duration = ScalableFloat.Constant(5f);
            thinker.PulseInterval = ScalableFloat.Constant(1f);
            return thinker;
        }

        private GameplayThinker CreateAuraThinker(float interval, System.Action<AuraBehaviour> configure = null)
        {
            var aura = new AuraBehaviour { Effect = _gas.CreateDurationEffect(10f) };
            configure?.Invoke(aura);
            return _gas.CreateThinkerPrefab(5f, interval, t => t.Behaviour = aura);
        }

        // Duração e raio lidos da tabela da habilidade, sem "termina com a execução"
        private GameplayThinker CreateTableThinker()
        {
            var aura = new AuraBehaviour { Effect = _gas.CreateDurationEffect(10f) };
            aura.Area.Radius = ScalableFloat.FromTable("raio");
            return _gas.CreateThinkerPrefab(0f, 1f, t =>
            {
                t.Duration = ScalableFloat.FromTable("vida");
                t.EndWithExecution = false;
                t.Behaviour = aura;
            });
        }

        private Projectile CreateProjectile()
        {
            var go = new GameObject("Projectile_Validation");
            _created.Add(go);
            return go.AddComponent<Projectile>();
        }

        private static ValidationResult Validate(Object target)
        {
            var result = new ValidationResult(target);
            ((IValidatable)target).Validate(result);
            return result;
        }

        private static List<string> Format(ValidationResult result)
        {
            return result.Messages.Select(m => $"{m.Severity}: {m.Message} ({m.PropertyPath})").ToList();
        }

        private static ValidationMessage AssertSingle(Object target, ValidationSeverity severity, string propertyPath)
        {
            ValidationResult result = Validate(target);

            Assert.That(Format(result), Has.Count.EqualTo(1));
            ValidationMessage message = result.Messages[0];
            Assert.That(message.Severity, Is.EqualTo(severity), message.Message);
            Assert.That(message.Context, Is.SameAs(target));
            Assert.That(message.PropertyPath, Is.EqualTo(propertyPath));
            Assert.That(new SerializedObject(target).FindProperty(propertyPath), Is.Not.Null, "o campo existe no objeto");
            return message;
        }
    }
}
