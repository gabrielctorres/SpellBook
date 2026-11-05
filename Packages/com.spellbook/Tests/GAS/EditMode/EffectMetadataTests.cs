using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Metadados, nome e ícone do <see cref="GameplayEffect"/> e validação de efeito e de <c>startingEffects</c>
    /// (B-005, AR7: CA29, CA30, CA30a, CA32; CA17 do B-018).
    /// </summary>
    public class EffectMetadataTests
    {
        private GasFactory _gas;
        private readonly List<Object> _created = new List<Object>();
        private string _legacyFolder;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
            if (_legacyFolder != null && AssetDatabase.IsValidFolder(_legacyFolder)) AssetDatabase.DeleteAsset(_legacyFolder);
            _legacyFolder = null;
        }

        [Test]
        public void Metadata_ReadFromAssetAndInstance_DefaultNeutral()
        {
            GameplayEffect neutral = _gas.CreateDurationEffect(5f);
            GameplayEffect marked = _gas.CreateDurationEffect(5f);
            SetBools(marked, ("isBuff", true), ("isHidden", true), ("isPurgable", true));

            var neutralInstance = new ActiveEffectInstance(neutral, null, null);
            var markedInstance = new ActiveEffectInstance(marked, null, null);

            Assert.That(new[] { neutral.IsBuff, neutral.IsDebuff, neutral.IsHidden, neutral.IsPurgable }, Is.All.False);
            Assert.That(new[] { neutralInstance.IsBuff, neutralInstance.IsDebuff, neutralInstance.IsHidden, neutralInstance.IsPurgable }, Is.All.False);
            Assert.That(new[] { marked.IsBuff, marked.IsDebuff, marked.IsHidden, marked.IsPurgable }, Is.EqualTo(new[] { true, false, true, true }));
            Assert.That(new[] { markedInstance.IsBuff, markedInstance.IsDebuff, markedInstance.IsHidden, markedInstance.IsPurgable }, Is.EqualTo(new[] { true, false, true, true }));
        }

        [Test]
        public void NameAndIcon_ReadFromAsset_DefaultEmptyAndNull()
        {
            GameplayEffect plain = _gas.CreateDurationEffect(5f);
            GameplayEffect named = _gas.CreateDurationEffect(5f);
            Sprite icon = CreateSprite();
            SetNameAndIcon(named, "Veneno", icon);

            Assert.That(plain.DisplayName.Text, Is.Not.Null.And.Empty);
            Assert.That(plain.Icon, Is.Null);
            Assert.That(named.DisplayName.Text, Is.EqualTo("Veneno"));
            Assert.That(named.Icon, Is.SameAs(icon));
        }

        [Test]
        public void NameAndIcon_ReadThroughInstanceEffect()
        {
            GameplayEffect named = _gas.CreateDurationEffect(5f);
            Sprite icon = CreateSprite();
            SetNameAndIcon(named, "Veneno", icon);

            var instance = new ActiveEffectInstance(named, null, null);

            Assert.That(instance.Effect.DisplayName.Text, Is.EqualTo("Veneno"));
            Assert.That(instance.Effect.Icon, Is.SameAs(icon));
        }

        [Test]
        public void OldEffectYaml_WithoutNameAndIcon_OpensEmpty()
        {
            GameplayEffect effect = ScalableMigrationTests.LoadLegacyEffect("GE_OldWithoutIdentity",
                "  DurationPolicy: 2\n  Duration: 4\n  Period: 0\n  CanStack: 1\n  MaxStacks: 3\n");
            _legacyFolder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(effect))?.Replace('\\', '/');

            Assert.That(effect.DisplayName.Text, Is.Not.Null.And.Empty);
            Assert.That(effect.Icon, Is.Null);
            Assert.That(new[] { effect.IsBuff, effect.IsDebuff, effect.IsHidden, effect.IsPurgable }, Is.All.False);
            Assert.That(effect.Duration.Evaluate(1), Is.EqualTo(4f), "o resto do asset abre sem perda");
            Assert.That(effect.MaxStacks, Is.EqualTo(3));
        }

        [Test]
        public void Defaults_NoMessages()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            GasFactory.SetObjectField(effect, "icon", CreateSprite());

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { effect }), Is.Empty);
        }

        [Test]
        public void BuffAndDebuff_Warning()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(5f);
            GasFactory.SetObjectField(effect, "icon", CreateSprite());
            SetBools(effect, ("isBuff", true), ("isDebuff", true));

            ValidationMessage message = SpellBookEditor.CollectMessages(new Object[] { effect }).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(message.PropertyPath, Is.EqualTo("isDebuff"));
        }

        [Test]
        public void StartingEffectsNullEntry_Error()
        {
            AbilitySystemComponent asc = CreateAscWithStartingEffects(_gas.CreateDurationEffect(5f), null);
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { asc.gameObject }, messages);

            ValidationMessage message = messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Context, Is.SameAs(asc));
            Assert.That(message.PropertyPath, Is.EqualTo("startingEffects.Array.data[1]"));
        }

        [Test]
        public void StartingEffectsFilled_NoMessages()
        {
            AbilitySystemComponent asc = CreateAscWithStartingEffects(_gas.CreateDurationEffect(5f));

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { asc }), Is.Empty);
        }

        private AbilitySystemComponent CreateAscWithStartingEffects(params GameplayEffect[] effects)
        {
            var go = new GameObject("ASC");
            _created.Add(go);
            var asc = go.AddComponent<AbilitySystemComponent>();
            var so = new SerializedObject(asc);
            SerializedProperty list = so.FindProperty("startingEffects");
            list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return asc;
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            _created.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero);
            _created.Add(sprite);
            return sprite;
        }

        private static void SetNameAndIcon(GameplayEffect effect, string displayName, Sprite icon)
        {
            var so = new SerializedObject(effect);
            so.FindProperty("localizedName.text").stringValue = displayName;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBools(GameplayEffect effect, params (string Field, bool Value)[] fields)
        {
            var so = new SerializedObject(effect);
            foreach ((string field, bool value) in fields) so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
