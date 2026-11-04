using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.UI.Editor;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Inspector do <c>AbilityLoadout</c> (R13 do PRD; Spec do B-016, Design 7): uma linha por entrada, na ordem da lista,
    /// que é a ordem da barra em edição (R12, decisão do usuário de 2026-11-04).
    /// </summary>
    public class AbilityLoadoutEditorTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null) Object.DestroyImmediate(_editor);
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void LoadoutRow_Summary_SlotAbilityCooldown()
        {
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            fireball.name = "Fireball";
            GameplayAbility dash = _gas.CreateAbility();
            dash.name = "Dash";
            AbilityLoadout loadout = Loadout((_gas.CreateTag("Slot.Q"), fireball), (_gas.CreateTag("Slot.W"), dash),
                (default, null));

            VisualElement root = CreateInspector(loadout);

            Assert.That(Rows(root), Is.EqualTo(new[]
            {
                "1. Slot.Q · Fireball · recarga 8 s",
                "2. Slot.W · Dash · sem recarga",
                "3. (nenhum) · (nenhum) · sem recarga"
            }));

            var serialized = new SerializedObject(loadout);
            serialized.FindProperty("entries").MoveArrayElement(1, 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ((AbilityLoadoutEditor)_editor).RefreshRows();

            Assert.That(Rows(root).Take(2), Is.EqualTo(new[]
            {
                "1. Slot.W · Dash · sem recarga",
                "2. Slot.Q · Fireball · recarga 8 s"
            }), "a ordem das linhas segue a lista, que é a ordem da barra");
        }

        private static string[] Rows(VisualElement root)
        {
            return root.Q(AbilityLoadoutEditor.RowsName).Query<Label>(className: AbilityLoadoutEditor.RowClass).ToList()
                .Select(label => label.text).ToArray();
        }

        private VisualElement CreateInspector(AbilityLoadout loadout)
        {
            _editor = UnityEditor.Editor.CreateEditor(loadout);
            return _editor.CreateInspectorGUI();
        }

        private AbilityLoadout Loadout(params (GameplayTag Slot, GameplayAbility Ability)[] entries)
        {
            var loadout = ScriptableObject.CreateInstance<AbilityLoadout>();
            _created.Add(loadout);
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
    }
}
