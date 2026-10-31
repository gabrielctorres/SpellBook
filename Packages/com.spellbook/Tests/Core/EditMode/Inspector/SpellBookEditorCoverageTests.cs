using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>
    /// Todo MonoBehaviour e ScriptableObject público e concreto de runtime usa um Inspector derivado de
    /// <see cref="SpellBookEditor"/> (Spec, seção 4.10, "Um editor por tipo"; R32, I48).
    /// </summary>
    public class SpellBookEditorCoverageTests
    {
        private static readonly string[] CoveredAssemblies =
        {
            "SpellBook.Core", "SpellBook.Time", "SpellBook.Tags", "SpellBook.Pooling",
            "SpellBook.GAS", "SpellBook.Events", "SpellBook.GAS.Bridges",
        };

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void EveryPublicConcreteMbAndSo_InRuntimeAssemblies_UsesSpellBookEditor()
        {
            List<Type> types = CoveredTypes();
            var violations = new List<string>();

            foreach (Type type in types)
            {
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(Instantiate(type));
                _created.Add(editor);
                if (!(editor is SpellBookEditor))
                {
                    violations.Add($"{type.FullName}: Inspector é {editor.GetType().FullName}");
                }
            }

            Assert.That(types.Select(t => t.Name), Does.Contain("PooledSpawnerAsset").And.Contain("ManualTimeSource")
                .And.Contain("AbilitySystemComponent").And.Contain("EventListener"),
                "a varredura não achou os tipos esperados");
            Assert.That(violations, Is.Empty);
        }

        private Object Instantiate(Type type)
        {
            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                ScriptableObject instance = ScriptableObject.CreateInstance(type);
                _created.Add(instance);
                return instance;
            }

            var host = new GameObject("CoverageHost");
            _created.Add(host);
            return host.AddComponent(type);
        }

        private static List<Type> CoveredTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => CoveredAssemblies.Contains(a.GetName().Name))
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsPublic && !t.IsAbstract && !t.IsGenericTypeDefinition)
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) || typeof(ScriptableObject).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();
        }
    }
}
