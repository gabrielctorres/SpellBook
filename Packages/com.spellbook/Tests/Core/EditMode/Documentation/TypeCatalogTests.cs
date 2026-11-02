using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>Catálogo de tipos lido do seletor do Inspector (Spec referencia-catalogo-e-api, D1; RC1).</summary>
    public class TypeCatalogTests
    {
        private static readonly Assembly TestAssembly = typeof(TypeCatalogTests).Assembly;

        [Test]
        public void Build_ListField_BaseIsElementType()
        {
            TypeCatalogSection section = SectionOf(TypeCatalog.Build(new[] { TestAssembly }), typeof(ICatalogFixture));

            Assert.That(section, Is.Not.Null);
            Assert.That(section.Entries.Select(e => e.Type),
                Is.EqualTo(new[] { typeof(CatalogFixtureA), typeof(CatalogFixtureB) }));
        }

        [Test]
        public void Build_SingleField_BaseIsFieldType()
        {
            TypeCatalogSection section = SectionOf(TypeCatalog.Build(new[] { TestAssembly }), typeof(CatalogSingleBase));

            Assert.That(section, Is.Not.Null);
            Assert.That(section.Fields, Is.EqualTo(new[] { "Catalog Single Holder > Single" }));
            Assert.That(section.Entries.Select(e => e.Type), Is.EqualTo(new[] { typeof(CatalogSingleImpl) }));
        }

        [Test]
        public void Build_FieldsUseInspectorNames()
        {
            TypeCatalogSection section = SectionOf(TypeCatalog.Build(new[] { TestAssembly }), typeof(ICatalogFixture));

            Assert.That(section.Fields, Is.EqualTo(new[] { "Holder > Extra", "Holder > Items" }));
        }

        [Test]
        public void Build_EntriesEqualSelectorCandidates()
        {
            IReadOnlyList<TypeCatalogSection> sections = TypeCatalog.Build();

            Assert.That(sections, Is.Not.Empty);
            foreach (TypeCatalogSection section in sections)
            {
                IEnumerable<Type> expected = TypePickerCatalog.GetCandidates(section.BaseType)
                    .Where(e => TypeCatalog.IsPackageAssembly(e.Type.Assembly.GetName().Name))
                    .Select(e => e.Type);
                Assert.That(section.Entries.Select(e => e.Type), Is.EqualTo(expected), section.BaseType.FullName);
            }
        }

        [Test]
        public void Build_HasPackageBases()
        {
            IEnumerable<string> bases = TypeCatalog.Build().Select(s => s.BaseType.FullName);

            Assert.That(bases, Is.SupersetOf(new[]
            {
                "SpellBook.Events.Reaction",
                "SpellBook.Events.EventResponse",
                "SpellBook.GAS.Abilities.AbilityAction",
            }));
        }

        [Test]
        public void Build_BaseWithoutPackageEntries_HasNoSection()
        {
            Assert.That(TypePickerCatalog.GetCandidates(typeof(StringBuilder)), Is.Not.Empty);

            IReadOnlyList<TypeCatalogSection> sections = TypeCatalog.Build(new[] { TestAssembly });

            Assert.That(SectionOf(sections, typeof(StringBuilder)), Is.Null);
        }

        [Test]
        public void IsPackageAssembly_ExcludesTestsTestingAndSamples()
        {
            Assert.That(TypeCatalog.IsPackageAssembly("SpellBook.GAS"), Is.True);
            Assert.That(TypeCatalog.IsPackageAssembly("SpellBook.GAS.Tests.EditMode"), Is.False);
            Assert.That(TypeCatalog.IsPackageAssembly("SpellBook.Testing"), Is.False);
            Assert.That(TypeCatalog.IsPackageAssembly("SpellBook.Samples.Sandbox"), Is.False);
            Assert.That(TypeCatalog.IsPackageAssembly("Assembly-CSharp"), Is.False);
        }

        [Test]
        public void Build_TwiceSameOrder()
        {
            string[] first = Describe(TypeCatalog.Build());
            string[] second = Describe(TypeCatalog.Build());

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.EqualTo(first));
        }

        private static TypeCatalogSection SectionOf(IEnumerable<TypeCatalogSection> sections, Type baseType)
        {
            return sections.SingleOrDefault(s => s.BaseType == baseType);
        }

        private static string[] Describe(IEnumerable<TypeCatalogSection> sections)
        {
            return sections.Select(s => s.BaseType.FullName + "|" + string.Join(";", s.Fields) + "|"
                + string.Join(";", s.Entries.Select(e => e.Type.FullName))).ToArray();
        }
    }

    internal interface ICatalogFixture
    {
    }

    [Serializable]
    internal class CatalogFixtureB : ICatalogFixture
    {
    }

    [Serializable]
    internal class CatalogFixtureA : ICatalogFixture
    {
    }

    [Serializable]
    internal abstract class CatalogSingleBase
    {
    }

    [Serializable]
    internal class CatalogSingleImpl : CatalogSingleBase
    {
    }

    [Serializable]
    internal class Holder
    {
        [SerializeReference, TypePicker] internal List<ICatalogFixture> items = new List<ICatalogFixture>();
        [SerializeReference, TypePicker] internal ICatalogFixture[] extra = Array.Empty<ICatalogFixture>();
    }

    [Serializable]
    internal class CatalogSingleHolder
    {
        [SerializeReference, TypePicker] internal CatalogSingleBase single;
    }

    [Serializable]
    internal class CatalogOrphanHolder
    {
        [SerializeReference, TypePicker] internal StringBuilder text;
    }
}
