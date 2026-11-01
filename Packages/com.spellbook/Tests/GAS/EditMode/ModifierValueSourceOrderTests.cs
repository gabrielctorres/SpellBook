using NUnit.Framework;
using SpellBook.GAS.Attributes;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Ordem serializada das fontes do modificador (B-007, VN6: CA19).</summary>
    public class ModifierValueSourceOrderTests
    {
        [Test]
        public void Enum_NewValuesAtEnd_OldAssetSameValue()
        {
            ModifierValueSource attribute = JsonUtility.FromJson<ModifierValueSource>("{\"Type\":1}");
            ModifierValueSource formula = JsonUtility.FromJson<ModifierValueSource>("{\"Type\":2}");

            Assert.That((int)ModifierValueSource.SourceType.Value, Is.EqualTo(0));
            Assert.That((int)ModifierValueSource.SourceType.Attribute, Is.EqualTo(1));
            Assert.That((int)ModifierValueSource.SourceType.Formula, Is.EqualTo(2));
            Assert.That((int)ModifierValueSource.SourceType.SetByCaller, Is.EqualTo(3));
            Assert.That((int)ModifierValueSource.SourceType.TargetAttribute, Is.EqualTo(4));
            Assert.That(attribute.Type, Is.EqualTo(ModifierValueSource.SourceType.Attribute), "asset antigo com Attribute");
            Assert.That(formula.Type, Is.EqualTo(ModifierValueSource.SourceType.Formula), "asset antigo com Formula");
        }
    }
}
