using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Inspector do <see cref="GameplayThinker"/> e do <see cref="Projectile"/> (B-014, TK8; R22, Design 7).</summary>
    public class ThinkerInspectorTests
    {
        private const string HeaderName = "spellbook-thinker-behaviour-header";

        private GasFactory _gas;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                SessionState.EraseString(ActionEventsView.SessionKey(created));
                Object.DestroyImmediate(created);
            }
            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Thinker_ThreeEventTabs_ListsLeaveDefaultInspector()
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 1f);

            VisualElement root = CreateInspector(thinker);

            Assert.That(root.Query<Tab>().ToList().Select(t => t.label), Is.EqualTo(new[] { "Ao criar", "A cada pulso", "Ao terminar" }));
            Assert.That(root.Q<PropertyField>("PropertyField:onPulse"), Is.Null, "a lista aparece também no Inspector padrão");
            Assert.That(root.Q<PropertyField>("PropertyField:behaviour"), Is.Not.Null, "o comportamento saiu do Inspector");
        }

        [Test]
        public void Thinker_AuraHeader_EffectRadiusIntervalFilter()
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            effect.name = "Bencao";
            var aura = new AuraBehaviour { Effect = effect };
            aura.Area.Radius = ScalableFloat.Constant(4f);
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 2f, t => t.Behaviour = aura);

            string withoutFilter = HeaderText(CreateInspector(thinker));
            aura.Area.Filter = new TagQueryCondition { Subject = ConditionSubject.TargetUnit };
            string filter;
            using (var serialized = new SerializedObject(thinker))
            {
                filter = SummaryFormatter.Format(serialized.FindProperty("behaviour.area.filter"));
            }
            string withFilter = HeaderText(CreateInspector(thinker));

            Assert.That(withoutFilter, Is.EqualTo($"Aura Bencao · raio 4 · intervalo 2 s · filtro: {SummaryFormatter.None}"));
            Assert.That(withFilter, Is.EqualTo($"Aura Bencao · raio 4 · intervalo 2 s · filtro: {filter}"));
            Assert.That(filter, Is.Not.EqualTo(SummaryFormatter.None));
        }

        [Test]
        public void Thinker_NoAura_NoHeader()
        {
            GameplayThinker thinker = _gas.CreateThinkerPrefab(0f, 1f);

            VisualElement header = CreateInspector(thinker).Q(HeaderName);

            Assert.That(header == null || header.style.display.value == DisplayStyle.None, Is.True, "cabeçalho sem aura");
        }

        [Test]
        public void Projectile_EndedTab()
        {
            var go = new GameObject("Projectile_Inspector");
            _created.Add(go);
            Projectile projectile = go.AddComponent<Projectile>();

            VisualElement root = CreateInspector(projectile);

            Assert.That(root.Query<Tab>().ToList().Select(t => t.label), Is.EqualTo(new[] { "Ao terminar" }));
            Assert.That(root.Q<PropertyField>("PropertyField:OnEnded"), Is.Null, "a lista aparece também no Inspector padrão");
        }

        private static string HeaderText(VisualElement root)
        {
            var header = root.Q<Label>(HeaderName);
            Assert.That(header, Is.Not.Null, "sem cabeçalho do comportamento");
            Assert.That(header.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            return header.text;
        }

        private VisualElement CreateInspector(Component target)
        {
            var editor = UnityEditor.Editor.CreateEditor(target);
            _created.Add(editor);
            return editor.CreateInspectorGUI();
        }
    }
}
