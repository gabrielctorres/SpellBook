using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>Modelo do popup do seletor de tipo (ADR 0026, item 4; Spec, seção 4.10; CA63).</summary>
    public class TypePickerTreeTests
    {
        [Test]
        public void Build_GroupsByCategoryPath_NoneFirst_CategoriesBeforeTypes()
        {
            TypePickerTree tree = SampleTree();

            TypePickerNode grupo = Child(tree, tree.Root, "Grupo");
            TypePickerNode sub = Child(tree, grupo, "Sub");

            Assert.That(Names(tree, tree.Root), Is.EqualTo(new[] { "(Nenhum)", "Arma", "Grupo", "Alpha", "Zeta" }));
            Assert.That(tree.Children(tree.Root).Select(n => n.Kind), Is.EqualTo(new[]
            {
                TypePickerNodeKind.None, TypePickerNodeKind.Category, TypePickerNodeKind.Category,
                TypePickerNodeKind.Type, TypePickerNodeKind.Type,
            }));
            Assert.That(Names(tree, grupo), Is.EqualTo(new[] { "Sub", "Beta" }));
            Assert.That(Names(tree, sub), Is.EqualTo(new[] { "Gamma" }));
            Assert.That(tree.Children(sub)[0].Type, Is.EqualTo(typeof(double)));
        }

        [Test]
        public void Build_TypeWithoutCategory_AtRoot()
        {
            TypePickerTree tree = TypePickerTree.Build(new[] { Entry(typeof(int), "Solto", string.Empty) });

            IReadOnlyList<TypePickerNode> root = tree.Children(tree.Root);

            Assert.That(root.Count, Is.EqualTo(2));
            Assert.That(root[0].Kind, Is.EqualTo(TypePickerNodeKind.None));
            Assert.That(root[0].Type, Is.Null);
            Assert.That(root[1].Kind, Is.EqualTo(TypePickerNodeKind.Type));
            Assert.That(root[1].Type, Is.EqualTo(typeof(int)));
        }

        [Test]
        public void Parent_ReturnsUpperLevel_RootHasNoParent()
        {
            TypePickerTree tree = SampleTree();
            TypePickerNode grupo = Child(tree, tree.Root, "Grupo");
            TypePickerNode sub = Child(tree, grupo, "Sub");
            TypePickerNode gamma = Child(tree, sub, "Gamma");

            Assert.That(tree.Parent(gamma), Is.SameAs(sub));
            Assert.That(tree.Parent(sub), Is.SameAs(grupo));
            Assert.That(tree.Parent(grupo), Is.SameAs(tree.Root));
            Assert.That(tree.Parent(tree.Root), Is.Null);
        }

        [Test]
        public void Search_ReturnsFlatMatchesWithPath()
        {
            TypePickerTree tree = SampleTree();

            IReadOnlyList<TypePickerEntry> byName = tree.Search("GAM");
            IReadOnlyList<TypePickerEntry> byPath = tree.Search("grupo");

            Assert.That(byName.Select(e => e.Type), Is.EqualTo(new[] { typeof(double) }));
            Assert.That(byName[0].CategoryPath, Is.EqualTo("Grupo/Sub"));
            Assert.That(byPath.Select(e => e.Type), Is.EquivalentTo(new[] { typeof(float), typeof(double) }));
        }

        [Test]
        public void Search_EmptyQuery_ReturnsEmpty()
        {
            TypePickerTree tree = SampleTree();

            Assert.That(tree.Search(string.Empty), Is.Empty);
            Assert.That(tree.Search(null), Is.Empty);
        }

        private static TypePickerTree SampleTree()
        {
            return TypePickerTree.Build(new[]
            {
                Entry(typeof(int), "Zeta", string.Empty),
                Entry(typeof(double), "Gamma", "Grupo/Sub"),
                Entry(typeof(string), "Alpha", string.Empty),
                Entry(typeof(float), "Beta", "Grupo"),
                Entry(typeof(bool), "Delta", "Arma"),
            });
        }

        private static TypePickerEntry Entry(Type type, string displayName, string categoryPath)
        {
            return new TypePickerEntry(type, displayName, categoryPath, string.Empty);
        }

        private static TypePickerNode Child(TypePickerTree tree, TypePickerNode level, string name)
        {
            return tree.Children(level).Single(n => n.DisplayName == name);
        }

        private static IEnumerable<string> Names(TypePickerTree tree, TypePickerNode level)
        {
            return tree.Children(level).Select(n => n.DisplayName);
        }
    }
}
