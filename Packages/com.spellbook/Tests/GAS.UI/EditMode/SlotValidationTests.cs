using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Validação do loadout, das fontes do slot e da barra e do binder de lista (Spec do B-016, Design 7; R14 e CA18 do
    /// PRD), cada mensagem com o objeto e o campo para o clique.
    /// </summary>
    public class SlotValidationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Loadout_Empty_IsWarning()
        {
            AbilityLoadout loadout = Loadout();

            AssertMessages(loadout, (ValidationSeverity.Warning, "entries",
                "Loadout vazio: a barra não mostra nenhum slot. Adicione uma entrada."));
        }

        [Test]
        public void Loadout_EntryWithoutTagOrAbility_IsError()
        {
            AbilityLoadout loadout = Loadout((default, Ability("Fireball")), (_gas.CreateTag("Slot.W"), null));

            AssertMessages(loadout,
                (ValidationSeverity.Error, "entries.Array.data[0].slot", "Entrada sem tag em entries (posição 0). Escolha a tag do slot."),
                (ValidationSeverity.Error, "entries.Array.data[1].ability",
                    "Entrada sem habilidade em entries (posição 1). Escolha a habilidade do slot."));
        }

        [Test]
        public void Loadout_RepeatedTag_IsError()
        {
            GameplayTag slotQ = _gas.CreateTag("Slot.Q");

            AbilityLoadout loadout = Loadout((slotQ, Ability("Fireball")), (_gas.CreateTag("Slot.W"), Ability("Heal")),
                (slotQ, Ability("Dash")));

            AssertMessages(loadout, (ValidationSeverity.Error, "entries.Array.data[2].slot",
                "A tag Slot.Q se repete em entries (posições 0 e 2): o slot mostra só a primeira. Use uma tag por entrada."));
        }

        [Test]
        public void Loadout_RepeatedAbility_IsWarning()
        {
            GameplayAbility fireball = Ability("Fireball");

            AbilityLoadout loadout = Loadout((_gas.CreateTag("Slot.Q"), fireball), (_gas.CreateTag("Slot.W"), fireball));

            AssertMessages(loadout, (ValidationSeverity.Warning, "entries.Array.data[1].ability",
                "A habilidade Fireball se repete em entries (posições 0 e 1): os dois slots mostram o mesmo estado."));
        }

        [Test]
        public void Loadout_AbilityWithoutIcon_IsWarning()
        {
            GameplayAbility fireball = Ability("Fireball");
            fireball.Icon = null;

            AbilityLoadout loadout = Loadout((_gas.CreateTag("Slot.Q"), fireball), (_gas.CreateTag("Slot.W"), fireball));

            AssertMessages(loadout,
                (ValidationSeverity.Warning, "entries.Array.data[0].ability",
                    "A habilidade Fireball não tem ícone (posição 0): o slot fica sem imagem."),
                (ValidationSeverity.Warning, "entries.Array.data[1].ability",
                    "A habilidade Fireball se repete em entries (posições 0 e 1): os dois slots mostram o mesmo estado."));
        }

        [Test]
        public void Loadout_ChargesInvalid_IsWarning()
        {
            GameplayAbility fireball = Ability("Fireball");
            fireball.Charges.Enabled = true;
            fireball.Charges.RechargeTime = ScalableFloat.Constant(0f);
            GameplayAbility heal = Ability("Heal");
            heal.Charges.Enabled = true;

            AbilityLoadout loadout = Loadout((_gas.CreateTag("Slot.Q"), fireball), (_gas.CreateTag("Slot.W"), heal));

            AssertMessages(loadout, (ValidationSeverity.Warning, "entries.Array.data[0].ability",
                "A habilidade Fireball tem cargas inválidas (posição 0): Recharge Time é 0: o tempo de recarga é maior que 0."));
        }

        [Test]
        public void Loadout_Valid_HasNoMessages()
        {
            AbilityLoadout loadout = Loadout((_gas.CreateTag("Slot.Q"), Ability("Fireball")), (_gas.CreateTag("Slot.W"), Ability("Heal")));

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { loadout }), Is.Empty);
        }

        [Test]
        public void SlotSource_NoLoadout_IsError()
        {
            AbilitySlotBinder binder = SlotBinder(new AbilitySlotSource { Slot = _gas.CreateTag("Slot.Q") });

            AssertMessages(binder, (ValidationSeverity.Error, "source.loadout",
                "Fonte sem loadout. Escolha o loadout com a entrada do slot."));
        }

        [Test]
        public void SlotSource_TagMissingOrOutsideLoadout_IsError()
        {
            AbilityLoadout loadout = Loadout((_gas.CreateTag("Slot.Q"), Ability("Fireball")));
            loadout.name = "Barra";

            AbilitySlotBinder noTag = SlotBinder(new AbilitySlotSource { Loadout = loadout });
            AbilitySlotBinder outside = SlotBinder(new AbilitySlotSource { Loadout = loadout, Slot = _gas.CreateTag("Slot.R") });
            AbilitySlotBinder inside = SlotBinder(new AbilitySlotSource { Loadout = loadout, Slot = loadout.Entries[0].Slot });

            AssertMessages(noTag, (ValidationSeverity.Error, "source.slot", "Fonte sem tag de slot. Escolha a tag procurada no loadout."));
            AssertMessages(outside, (ValidationSeverity.Error, "source.slot",
                "A tag Slot.R não está no loadout Barra. Escolha a tag de uma das entradas dele."));
            Assert.That(SpellBookEditor.CollectMessages(new Object[] { inside }), Is.Empty);
        }

        [Test]
        public void BarSource_NoLoadout_IsError()
        {
            AbilityBarBinder binder = BarBinder(new AbilityLoadoutSource(), SlotPrefab(withView: true));

            AssertMessages(binder, (ValidationSeverity.Error, "source.loadout",
                "Fonte sem loadout. Escolha o loadout com as entradas da barra."));
        }

        [Test]
        public void Bar_NoPrefabOrPrefabWithoutSlotView_IsError()
        {
            AbilityBarBinder noSource = BarBinder(null, SlotPrefab(withView: true));
            AbilityBarBinder noPrefab = BarBinder(BarSource(), null);
            GameObject plain = SlotPrefab(withView: false);
            AbilityBarBinder noView = BarBinder(BarSource(), plain);

            AssertMessages(noSource, (ValidationSeverity.Error, "source", "Sem fonte. Escolha o que o binder lê do dono."));
            AssertMessages(noPrefab, (ValidationSeverity.Error, "itemPrefab", "Sem prefab de item. Ligue o prefab com a view do item."));
            AssertMessages(noView, (ValidationSeverity.Error, "itemPrefab",
                $"O prefab {plain.name} não tem na raiz um componente que implemente IView<SlotModel>. Ligue um prefab que desenhe esse modelo."));
        }

        [Test]
        public void Bar_Valid_HasNoMessages()
        {
            AbilityBarBinder binder = BarBinder(BarSource(), SlotPrefab(withView: true));

            Assert.That(SpellBookEditor.CollectMessages(new Object[] { binder }), Is.Empty);
        }

        // Cada mensagem com a severidade, o caminho, o texto e o objeto validado como contexto, para o clique
        private static void AssertMessages(Object target, params (ValidationSeverity Severity, string Path, string Text)[] expected)
        {
            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new[] { target });

            Assert.That(messages.Select(m => m.Message).ToArray(), Is.EqualTo(expected.Select(e => e.Text).ToArray()));
            Assert.That(messages.Select(m => m.Severity).ToArray(), Is.EqualTo(expected.Select(e => e.Severity).ToArray()));
            Assert.That(messages.Select(m => m.PropertyPath).ToArray(), Is.EqualTo(expected.Select(e => e.Path).ToArray()));
            Assert.That(messages.Select(m => m.Context), Has.All.SameAs(target), "a mensagem precisa apontar o objeto, para o clique");
        }

        private AbilityLoadoutSource BarSource()
        {
            var source = new AbilityLoadoutSource();
            source.SetLoadout(Loadout((_gas.CreateTag("Slot.Q"), Ability("Fireball"))));
            return source;
        }

        private AbilitySlotBinder SlotBinder(AbilitySlotSource source)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            var binder = asc.gameObject.AddComponent<AbilitySlotBinder>();
            binder.SetView(asc.gameObject.AddComponent<SlotRecorder>());
            binder.SetSource(source);
            return binder;
        }

        private AbilityBarBinder BarBinder(AbilityLoadoutSource source, GameObject itemPrefab)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            var binder = asc.gameObject.AddComponent<AbilityBarBinder>();
            binder.SetSource(source);
            GasFactory.SetObjectField(binder, "itemPrefab", itemPrefab);
            return binder;
        }

        private GameObject SlotPrefab(bool withView)
        {
            GameObject prefab = Track(new GameObject(withView ? "Slot" : "Plain"));
            if (withView) prefab.AddComponent<SlotRecorder>();
            return prefab;
        }

        // Habilidade com nome e ícone; o teste tira o ícone quando precisa
        private GameplayAbility Ability(string name)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.name = name;
            Texture2D texture = Track(new Texture2D(1, 1));
            ability.Icon = Track(Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero));
            return ability;
        }

        /// <summary>Loadout com as entradas na ordem dada, gravadas pelo <see cref="SerializedObject"/>.</summary>
        private AbilityLoadout Loadout(params (GameplayTag Slot, GameplayAbility Ability)[] entries)
        {
            AbilityLoadout loadout = Track(ScriptableObject.CreateInstance<AbilityLoadout>());
            var serialized = new SerializedObject(loadout);
            SerializedProperty list = serialized.FindProperty("entries");
            for (int i = 0; i < entries.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("slot.definition").objectReferenceValue = entries[i].Slot.GetDefinition();
                entry.FindPropertyRelative("ability").objectReferenceValue = entries[i].Ability;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return loadout;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        public sealed class SlotRecorder : MonoBehaviour, IView<SlotModel>
        {
            public void Render(in SlotModel model)
            {
            }
        }
    }
}
