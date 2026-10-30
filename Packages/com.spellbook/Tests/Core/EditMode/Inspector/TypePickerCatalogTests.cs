using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>Catálogo do seletor de tipo (ADR 0026, item 4; Spec, seção 4.10; CA63).</summary>
    public class TypePickerCatalogTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object created in _created.Where(o => o != null))
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Candidates_ExcludeAbstractGenericUnityObjectNonSerializableAndNoDefaultCtor()
        {
            IEnumerable<Type> types = TypePickerCatalog.GetCandidates(typeof(ITestPick)).Select(e => e.Type);

            Assert.That(types, Is.EquivalentTo(new[] { typeof(PickConcrete), typeof(PickCategorized) }));
        }

        [Test]
        public void Candidates_IncludeConcreteBaseItself()
        {
            IEnumerable<Type> types = TypePickerCatalog.GetCandidates(typeof(PickConcreteRoot)).Select(e => e.Type);

            Assert.That(types, Is.EquivalentTo(new[] { typeof(PickConcreteRoot), typeof(PickConcreteChild) }));
        }

        [Test]
        public void Candidates_IncludeDerivedFromAnotherAssembly()
        {
            IEnumerable<Type> types = TypePickerCatalog.GetCandidates(typeof(EffectAction)).Select(e => e.Type);

            Assert.That(typeof(PickEffectAction).Assembly, Is.Not.EqualTo(typeof(EffectAction).Assembly));
            Assert.That(types, Does.Contain(typeof(PickEffectAction)));
        }

        [Test]
        public void Category_FromTypeCategory_RootWhenMissing()
        {
            IReadOnlyList<TypePickerEntry> entries = TypePickerCatalog.GetCandidates(typeof(PickBase));

            Assert.That(Entry(entries, typeof(PickCategorized)).CategoryPath, Is.EqualTo("Grupo/Sub"));
            Assert.That(Entry(entries, typeof(PickConcrete)).CategoryPath, Is.Empty);
            Assert.That(Entry(entries, typeof(PickConcrete)).DisplayName, Is.EqualTo("Pick Concrete"));
        }

        [Test]
        public void Summary_FromTypeCategory()
        {
            IReadOnlyList<TypePickerEntry> entries = TypePickerCatalog.GetCandidates(typeof(PickBase));

            Assert.That(Entry(entries, typeof(PickCategorized)).Summary, Is.EqualTo("Resumo de teste"));
            Assert.That(Entry(entries, typeof(PickConcrete)).Summary, Is.Empty);
        }

        [Test]
        public void Filter_ByDisplayNameTypeNameAndCategory_CaseInsensitive()
        {
            IReadOnlyList<TypePickerEntry> entries = TypePickerCatalog.GetCandidates(typeof(PickBase));

            Assert.That(FilteredTypes(entries, "CONCRETE"), Is.EqualTo(new[] { typeof(PickConcrete) }));
            Assert.That(FilteredTypes(entries, "pickcategorized"), Is.EqualTo(new[] { typeof(PickCategorized) }));
            Assert.That(FilteredTypes(entries, "grupo/SUB"), Is.EqualTo(new[] { typeof(PickCategorized) }));
            Assert.That(FilteredTypes(entries, "inexistente"), Is.Empty);
        }

        [Test]
        public void Assign_CreatesInstanceOfChosenType_NullClears()
        {
            PickHolder holder = CreateHolder();
            SerializedProperty single = new SerializedObject(holder).FindProperty("single");
            Assert.That(TypePickerCatalog.ResolveFieldBaseType(single), Is.EqualTo(typeof(PickBase)));

            TypePickerCatalog.Assign(single, typeof(PickCategorized));
            Assert.That(holder.single, Is.InstanceOf<PickCategorized>());

            TypePickerCatalog.Assign(single, null);
            Assert.That(holder.single, Is.Null);
        }

        [Test]
        public void EnsureUnique_SharedReferenceInList_BecomesIndependentCopy()
        {
            PickHolder holder = CreateHolder();
            var shared = new PickConcrete { Value = 7 };
            holder.items.Add(shared);
            holder.items.Add(shared);
            SerializedProperty items = new SerializedObject(holder).FindProperty("items");

            Assert.That(TypePickerCatalog.EnsureUniqueInArray(items.GetArrayElementAtIndex(0)), Is.False);
            Assert.That(TypePickerCatalog.EnsureUniqueInArray(items.GetArrayElementAtIndex(1)), Is.True);

            holder.items[0].Value = 99;
            Assert.That(holder.items[1], Is.InstanceOf<PickConcrete>());
            Assert.That(holder.items[1].Value, Is.EqualTo(7));
        }

        private PickHolder CreateHolder()
        {
            var holder = ScriptableObject.CreateInstance<PickHolder>();
            _created.Add(holder);
            return holder;
        }

        private static TypePickerEntry Entry(IEnumerable<TypePickerEntry> entries, Type type)
        {
            return entries.Single(e => e.Type == type);
        }

        private static Type[] FilteredTypes(IEnumerable<TypePickerEntry> entries, string query)
        {
            return TypePickerCatalog.Filter(entries, query).Select(e => e.Type).ToArray();
        }
    }

    internal interface ITestPick { }

    [Serializable]
    internal abstract class PickBase : ITestPick
    {
        public int Value;
    }

    [Serializable]
    internal class PickConcrete : PickBase { }

    [Serializable]
    [TypeCategory("Grupo/Sub", Summary = "Resumo de teste")]
    internal class PickCategorized : PickBase { }

    [Serializable]
    internal class PickGeneric<T> : PickBase { }

    [Serializable]
    internal abstract class PickAbstract : PickBase { }

    internal class PickNotSerializable : PickBase { }

    [Serializable]
    internal class PickNoDefaultCtor : PickBase
    {
        public PickNoDefaultCtor(int value)
        {
            Value = value;
        }
    }

    [Serializable]
    internal class PickConcreteRoot { }

    [Serializable]
    internal class PickConcreteChild : PickConcreteRoot { }

    [Serializable]
    internal class PickEffectAction : EffectAction
    {
        public override void Execute(AbilitySystemComponent source, AbilitySystemComponent target,
            ActiveEffectInstance instance = null)
        {
        }
    }
}
