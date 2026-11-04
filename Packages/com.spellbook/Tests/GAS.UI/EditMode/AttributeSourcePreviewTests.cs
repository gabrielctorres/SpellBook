using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Attributes;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>Prévia das fontes de atributo em edição: 50 de 100, no formato do atributo e na cultura da prévia.</summary>
    public class AttributeSourcePreviewTests
    {
        [Test]
        public void Preview_50Of100()
        {
            AttributeDefinition vida = ScriptableObject.CreateInstance<AttributeDefinition>();
            vida.Display.Decimals = 1;
            vida.Display.Role = TextRole.Harmful;
            var context = new UIPreviewContext(1, null, CultureInfo.GetCultureInfo("pt-BR"));
            try
            {
                BarModel bar = new AttributeBarSource { Current = vida }.CreatePreview(context);
                TextModel text = new AttributeTextSource { Current = vida }.CreatePreview(context);

                Assert.That(bar.Current, Is.EqualTo(50f));
                Assert.That(bar.Max, Is.EqualTo(100f));
                Assert.That(bar.Fraction, Is.EqualTo(0.5f));
                Assert.That(bar.Role, Is.EqualTo(TextRole.Harmful));
                Assert.That(bar.Text.Buffer.ToString(bar.Text), Is.EqualTo("50,0 / 100,0"));
                Assert.That(text.Text.Buffer.ToString(text.Text), Is.EqualTo("50,0"));
                Assert.That(text.Role, Is.EqualTo(TextRole.Harmful));
            }
            finally
            {
                Object.DestroyImmediate(vida);
            }
        }

        /// <summary>
        /// O seletor de tipos do campo de fonte do binder mostra só o caminho e o resumo do <c>[TypeCategory]</c>, e o
        /// catálogo pula esse campo (genérico no <c>UIBinder</c>); por isso a guarda fica aqui (ADR 0013, item 4).
        /// </summary>
        [Test]
        public void EveryConcreteUISource_HasTypeCategoryWithSummary()
        {
            Type[] sources = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("SpellBook.", StringComparison.Ordinal)
                    && !assembly.GetName().Name.Contains(".Tests"))
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(UISourceBase).IsAssignableFrom(type) && !type.IsAbstract && !type.IsGenericTypeDefinition)
                .ToArray();
            string[] missing = sources
                .Where(type => string.IsNullOrWhiteSpace(
                    ((TypeCategoryAttribute)Attribute.GetCustomAttribute(type, typeof(TypeCategoryAttribute)))?.Summary))
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(sources, Is.SupersetOf(new[] { typeof(AttributeBarSource), typeof(AttributeTextSource) }));
            Assert.That(missing, Is.Empty,
                "fontes de UI sem resumo; ponha [TypeCategory(caminho, Summary = \"...\")] em:\n" + string.Join("\n", missing));
        }
    }
}
