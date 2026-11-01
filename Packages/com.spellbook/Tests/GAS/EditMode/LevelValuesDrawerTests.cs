using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Inspector dos valores por nível (Spec de valores por nível, VN10): o drawer do <see cref="ScalableFloat"/> e o
    /// "nível de prévia" da habilidade. Confere a estrutura e os efeitos no asset, sem painel.
    /// </summary>
    public class LevelValuesDrawerTests
    {
        private GasFactory _gas;
        private SerializedObject _serializedObject;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
            }

            _serializedObject?.Dispose();
            _gas.Dispose();
        }

        [Test]
        public void ScalableDrawer_InvalidText_KeepsLastValid_ShowsError()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(0f);
            effect.Duration = ScalableFloat.FromArray(10f, 8f, 6f, 4f);
            ScalableFloatField field = Draw(effect, NoAbilities);
            var text = field.Q<TextField>(ScalableFloatDrawer.ArrayTextName);
            var error = field.Q<HelpBox>(ScalableFloatDrawer.ArrayErrorName);
            Assert.That(text.value, Is.EqualTo("10 8 6 4"));
            Assert.That(error.style.display.value, Is.EqualTo(DisplayStyle.None));

            field.SubmitArrayText("10 x 6");

            Assert.That(effect.Duration.Evaluate(2), Is.EqualTo(8f), "o texto inválido apagou o último valor válido");
            Assert.That(effect.Duration.Evaluate(4), Is.EqualTo(4f));
            Assert.That(error.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(error.text, Does.Contain("\"x\""));

            field.SubmitArrayText("5; 4,3");

            Assert.That(effect.Duration.Evaluate(2), Is.EqualTo(4f));
            Assert.That(effect.Duration.Evaluate(9), Is.EqualTo(3f));
            Assert.That(error.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ScalableDrawer_TableMode_OffersOnlyTableNames_NoFreeText()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(0f);
            effect.Duration = ScalableFloat.FromTable("cooldown");
            GameplayAbility user = _gas.CreateAbility();
            user.CooldownEffect = effect;
            user.Values.Add("damage", ScalableFloat.FromArray(50f, 75f));
            user.Values.Add("cooldown", ScalableFloat.FromArray(8f, 7f));
            GameplayAbility other = _gas.CreateAbility();
            other.Values.Add("range", ScalableFloat.Constant(5f));

            ScalableFloatField field = Draw(effect, () => new[] { user, other });

            var names = field.Q<DropdownField>(ScalableFloatDrawer.TableNameName);
            Assert.That(names, Is.Not.Null, "falta a lista de nomes da tabela");
            Assert.That(names.choices, Is.EqualTo(new[] { "cooldown", "damage" }));
            Assert.That(names.value, Is.EqualTo("cooldown"));
            VisualElement table = field.Q(ScalableFloatDrawer.TableSectionName);
            Assert.That(table.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(table.Query<TextField>().ToList(), Is.Empty, "o modo Table não aceita nome livre");
            Assert.That(field.Q(ScalableFloatDrawer.ArraySectionName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AbilityEditor_PreviewLevelUpdatesPreview()
        {
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(0f);
            fireball.CooldownEffect.Duration = ScalableFloat.FromTable("cooldown");
            fireball.Values.Add("damage", ScalableFloat.FromArray(50f, 75f, 100f, 125f));
            fireball.Values.Add("cooldown", ScalableFloat.FromArray(8f, 7f, 6f, 5f));
            using (var serialized = new SerializedObject(fireball))
            {
                serialized.FindProperty("maxLevel").intValue = 4;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            _editor = UnityEditor.Editor.CreateEditor(fireball);
            VisualElement root = _editor.CreateInspectorGUI();
            var editor = (GameplayAbilityEditor)_editor;
            var level = root.Q<IntegerField>(GameplayAbilityEditor.PreviewLevelName);
            var preview = root.Q<Label>(GameplayAbilityEditor.PreviewName);
            Assert.That(level.value, Is.EqualTo(1));
            Assert.That(preview.text, Does.Contain("damage = 50"));

            editor.SetPreviewLevel(3);

            Assert.That(level.value, Is.EqualTo(3));
            Assert.That(preview.text, Does.Contain("damage = 100"));
            Assert.That(preview.text, Does.Contain("Recarga = 6 s"));

            editor.SetPreviewLevel(9);

            Assert.That(level.value, Is.EqualTo(4), "o nível de prévia fica em [1, MaxLevel]");
            Assert.That(preview.text, Does.Contain("damage = 125"));
        }

        private static IEnumerable<GameplayAbility> NoAbilities()
        {
            return new GameplayAbility[0];
        }

        private ScalableFloatField Draw(GameplayEffect effect, System.Func<IEnumerable<GameplayAbility>> abilities)
        {
            _serializedObject = new SerializedObject(effect);
            return new ScalableFloatField(_serializedObject.FindProperty("duration"), abilities);
        }
    }
}
