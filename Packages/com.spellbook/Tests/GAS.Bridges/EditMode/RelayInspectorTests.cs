using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Inspector do <see cref="AbilitySystemEventRelay"/> (Spec do relay, RL5; Design 5): os pares ficam em
    /// <see cref="Foldout"/>s fechados por grupo, fora do Inspector padrão. Confere a estrutura, sem bind.
    /// </summary>
    public class RelayInspectorTests
    {
        private static readonly string[][] Groups =
        {
            new[] { "effectApplied", "effectRemoved", "effectStacksChanged", "effectBlocked" },
            new[] { "abilityActivated", "abilityEnded", "abilityFailed", "abilityGranted", "abilityRemoved" },
            new[] { "attributeChanged", "attributeDecreased", "decreasedAttribute" },
            new[] { "tagAdded", "tagRemoved" },
            new[] { "blocked" },
        };

        private GameObject _go;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Relay");
            _go.SetActive(false);
            _editor = UnityEditor.Editor.CreateEditor(_go.AddComponent<AbilitySystemEventRelay>());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_editor);
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Inspector_GroupsPairsInFoldouts()
        {
            VisualElement root = _editor.CreateInspectorGUI();

            Foldout[] foldouts = root.Query<Foldout>().ToList().ToArray();
            Assert.That(_editor, Is.InstanceOf<SpellBookEditor>());
            Assert.That(foldouts.Select(f => f.text), Is.EqualTo(new[] { "Efeitos", "Habilidades", "Atributos", "Tags", "Bloqueio" }));
            Assert.That(foldouts.Select(f => f.value), Has.All.False);
            for (int i = 0; i < Groups.Length; i++)
            {
                string[] paths = foldouts[i].Query<PropertyField>().ToList().Select(f => f.bindingPath).ToArray();
                Assert.That(paths, Is.EqualTo(Groups[i]), foldouts[i].text);
            }
        }

        [Test]
        public void Inspector_DefaultFieldsKeepOnlyTheAsc()
        {
            VisualElement root = _editor.CreateInspectorGUI();

            string[] outside = root.Query<PropertyField>().ToList()
                .Where(f => f.GetFirstAncestorOfType<Foldout>() == null)
                .Select(f => f.bindingPath)
                .ToArray();

            Assert.That(outside, Does.Contain("abilitySystem"));
            Assert.That(outside.Intersect(Groups.SelectMany(g => g)), Is.Empty);
        }

        [Test]
        public void Inspector_RecentFirings_AfterDefault_HiddenOutsidePlay()
        {
            VisualElement root = _editor.CreateInspectorGUI();

            VisualElement recent = root.Q(className: "spellbook-relay-recent");
            Assert.That(recent, Is.Not.Null, "falta a seção dos últimos disparos");
            Assert.That(root.IndexOf(recent), Is.EqualTo(root.childCount - 1), "a seção não vem depois do Inspector padrão");
            Assert.That(recent.style.display.value, Is.EqualTo(DisplayStyle.None), "fora do Play a seção deveria ficar oculta");
        }
    }
}
