using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.Cues;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// <see cref="ExecuteCueAction"/> nas listas da habilidade e do efeito (B-013, CU8a: CA13, CA15). Os ASCs têm
    /// <c>autoTick</c> desligado e o roteador avança só por <c>Tick</c>.
    /// </summary>
    public class CueActionsTests
    {
        private GasFactory _gas;
        private CuesFactory _cues;
        private SpawnFactory _spawns;
        private TimeFactory _times;
        private ManualTimeSource _time;
        private RecordingSpawnerAsset _spawner;
        private CueRouterAnchor _anchor;
        private GameplayTag _tag;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _cues = new CuesFactory();
            _spawns = new SpawnFactory();
            _times = new TimeFactory();
            _time = _times.CreateManual();
            _spawner = _cues.CreateSpawner();
            _anchor = _cues.CreateAnchor();
            _tag = _cues.CreateTag("Cue.Test");
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _cues.Dispose();
            _spawns.Dispose();
            _times.Dispose();
        }

        [Test]
        public void ExecuteCue_InAbility_PlaysWithContextParameters()
        {
            RecordingItem item = RouterWithRecording();
            var magnitude = new CueMagnitude
            {
                Mode = CueMagnitude.MagnitudeMode.Value, Value = ScalableFloat.FromArray(2f, 4f), Max = 8f,
            };
            GameplayAbility ability = _gas.CreateAbility(new ExecuteCueAction { Router = _anchor, Tag = _tag, Magnitude = magnitude });
            using (var so = new SerializedObject(ability))
            {
                so.FindProperty("maxLevel").intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AbilitySystemComponent caster = CreateAsc();
            caster.GiveAbility(ability, 2);
            AbilitySystemComponent target = CreateAsc();
            target.transform.position = new Vector3(3f, 0f, 1f);

            caster.TryActivateAbility(ability, AbilityTarget.AtUnit(target));

            Assert.That(item.Played, Has.Count.EqualTo(1));
            CueParameters played = item.Played[0];
            Assert.That(played.Position, Is.EqualTo(target.transform.position));
            Assert.That(played.Target, Is.SameAs(target.transform));
            Assert.That(played.Instigator, Is.SameAs(caster.transform));
            Assert.That(played.HasMagnitude, Is.True);
            Assert.That(played.Magnitude, Is.EqualTo(4f), "a magnitude não foi avaliada no nível da ativação");
            Assert.That(played.NormalizedMagnitude, Is.EqualTo(0.5f));
        }

        [Test]
        public void ExecuteCue_InEffectTick_UsesEffectMagnitudeBySetByCaller()
        {
            RecordingItem item = RouterWithRecording();
            var magnitude = new CueMagnitude { Mode = CueMagnitude.MagnitudeMode.SetByCaller, CallerName = "Dano", Max = 20f };
            // A instância guarda só os valores que os modificadores dela leem (ADR 0030): o cue mostra o dano do tique
            AttributeDefinition health = _gas.CreateAttribute("Health");
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.Period = 1f;
            effect.PeriodicModifiers.Add(new AttributeEffectModifier
            {
                Attribute = health, Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.SetByCaller, CallerName = "Dano" },
            });
            effect.OnTick.Add(new ExecuteCueAction { Router = _anchor, Tag = _tag, Magnitude = magnitude });
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent target = CreateAsc(health);
            var values = new SetByCallerValues();
            values.Set("Dano", 5f);

            target.ApplyEffect(EffectSpec.Create(effect, caster).WithCallerValues(values));
            item.Played.Clear();
            target.Tick(1f);

            Assert.That(item.Played, Has.Count.EqualTo(1), "o tique não tocou o cue uma vez");
            Assert.That(item.Played[0].Target, Is.SameAs(target.transform), "no efeito, o alvo padrão é o dono");
            Assert.That(item.Played[0].Instigator, Is.SameAs(caster.transform));
            Assert.That(item.Played[0].Magnitude, Is.EqualTo(5f));
            Assert.That(item.Played[0].NormalizedMagnitude, Is.EqualTo(0.25f));
        }

        [Test]
        public void EmptyAnchor_WarnsOnce_NoException()
        {
            GameplayAbility ability = _gas.CreateAbility(new ExecuteCueAction { Router = _anchor, Tag = _tag });
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);

            LogAssert.Expect(LogType.Warning, new Regex("ExecuteCueAction.*roteador"));
            asc.TryActivateAbility(spec, Vector3.zero);
            asc.TryActivateAbility(spec, Vector3.zero);

            Assert.That(spec.IsActive, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SpawnerEmpty_InEffectCue_SkipsWithWarning()
        {
            CueRouter router = Router(_cues.CreateCue(_tag, CueKind.Burst,
                new VfxCueItem { Prefab = _spawns.CreatePrefab(), End = CueEnd.FixedDuration }));
            _spawner.ReturnsNull = true;
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.OnApplied.Add(new ExecuteCueAction { Router = _anchor, Tag = _tag });
            AbilitySystemComponent target = CreateAsc();

            LogAssert.Expect(LogType.Warning, new Regex("devolveu null"));
            ActiveEffectInstance instance = target.ApplyEffect(effect);

            Assert.That(instance, Is.Not.Null, "o efeito não foi aplicado");
            Assert.That(_spawner.SpawnCount, Is.EqualTo(0));
            Assert.That(router.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Validate_InvalidTagError_EmptyAnchorWarning()
        {
            var empty = new ValidationResult(null);
            var configured = new ValidationResult(null);
            var badMagnitude = new ValidationResult(null);

            new ExecuteCueAction().Validate(empty);
            new ExecuteCueAction { Router = _anchor, Tag = _tag }.Validate(configured);
            new ExecuteCueAction
            {
                Router = _anchor, Tag = _tag,
                Magnitude = new CueMagnitude { Mode = CueMagnitude.MagnitudeMode.SetByCaller, CallerName = "", Max = 0f },
            }.Validate(badMagnitude);

            Assert.That(empty.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.PropertyPath),
                Is.EqualTo(new[] { "tag" }));
            Assert.That(empty.Messages.Where(m => m.Severity == ValidationSeverity.Warning).Select(m => m.PropertyPath),
                Is.EqualTo(new[] { "router" }));
            Assert.That(configured.Messages, Is.Empty);
            Assert.That(badMagnitude.Messages.Select(m => m.PropertyPath),
                Is.EquivalentTo(new[] { "magnitude.callerName", "magnitude.max" }));
        }

        private AbilitySystemComponent CreateAsc(AttributeDefinition attribute = null)
        {
            AbilitySystemComponent asc = attribute != null ? _gas.CreateAsc((attribute, 0f)) : _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time);
            return asc;
        }

        private CueRouter Router(params CueDefinition[] cues) => _cues.CreateRouter(_time, _spawner, _anchor, cues);

        private RecordingItem RouterWithRecording()
        {
            CueDefinition cue = _cues.CreateCue(_tag, CueKind.Burst, new RecordingItem());
            Router(cue);
            // A definição guarda uma cópia serializada do item: o registro é o da cópia
            var item = (RecordingItem)cue.Items[0];
            item.Played.Clear();
            return item;
        }

        /// <summary>Item que só registra os parâmetros com que tocou.</summary>
        [Serializable]
        public sealed class RecordingItem : CueItem
        {
            [NonSerialized] private List<CueParameters> _played;

            public List<CueParameters> Played => _played ??= new List<CueParameters>();

            public override void Play(in CueParameters parameters, CuePlayback playback) => Played.Add(parameters);
        }
    }
}
