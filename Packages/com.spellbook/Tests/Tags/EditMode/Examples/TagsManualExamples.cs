// --8<-- [start:using-system-text]
using System.Text;
// --8<-- [end:using-system-text]
using NUnit.Framework;
using SpellBook.Core.Validation;
// --8<-- [start:using-tags]
using SpellBook.Tags;
// --8<-- [end:using-tags]
using UnityEditor;
// --8<-- [start:using-unityengine]
using UnityEngine;
// --8<-- [end:using-unityengine]
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.Tags.Tests.Examples
{
    /// <summary>
    /// Exemplos da página <c>modulos/tags.md</c> do manual. Cada trecho entre os marcadores <c>--8&lt;--</c> aparece na
    /// página, e os testes conferem o que o texto em volta diz dele (guia do manual, parte 9).
    /// </summary>
    public class TagsManualExamples
    {
        private TagTestFactory _factory;
        private GameplayTagDefinition stunnedDefinition;
        private GameplayTagDefinition slowedDefinition;
        private GameplayTagDefinition immuneDefinition;
        private GameplayTagDefinition fireDefinition;
        private GameplayTagDefinition fireFlameDefinition;
        private GameplayTagDefinition iceDefinition;

        [SetUp]
        public void SetUp()
        {
            _factory = new TagTestFactory();
            GameplayTagDefinition state = _factory.Create("State");
            GameplayTagDefinition debuff = _factory.Create("State.Debuff", state);
            stunnedDefinition = _factory.Create("State.Stunned", state);
            slowedDefinition = _factory.Create("State.Debuff.Slowed", debuff);
            immuneDefinition = _factory.Create("State.Imune", state);
            GameplayTagDefinition element = _factory.Create("Elemento");
            fireDefinition = _factory.Create("Elemento.Fogo", element);
            fireFlameDefinition = _factory.Create("Elemento.Fogo.Chama", fireDefinition);
            iceDefinition = _factory.Create("Elemento.Gelo", element);
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void GameplayTag_FromDefinition_IsValidWithDefinitionName()
        {
            // --8<-- [start:create-tag]

            var stunned = new GameplayTag(stunnedDefinition);
            // --8<-- [end:create-tag]

            Assert.That(stunned.IsValid, Is.True);
            Assert.That(stunned.Name, Is.EqualTo("State.Stunned"));
            Assert.That(stunned.GetDefinition(), Is.SameAs(stunnedDefinition));
        }

        [Test]
        public void GameplayTagContainer_AddRemoveHasTag()
        {
            var fireFlame = new GameplayTag(fireFlameDefinition);
            var stunned = new GameplayTag(stunnedDefinition);
            var slowed = new GameplayTag(slowedDefinition);
            var fire = new GameplayTag(fireDefinition);

            // --8<-- [start:container]

            var tags = new GameplayTagContainer(fireFlame, stunned);   // repetidas e inválidas são ignoradas
            bool entrou = tags.Add(slowed);        // true: entrou no fim
            bool entrouDeNovo = tags.Add(slowed);  // false: já estava
            bool saiu = tags.Remove(stunned);      // true: saiu (só a tag exata; filhas dela ficam)

            bool burning = tags.HasTag(fire);   // true se o conjunto tem Elemento.Fogo ou uma filha dele
            // --8<-- [end:container]

            Assert.That(entrou, Is.True);
            Assert.That(entrouDeNovo, Is.False);
            Assert.That(saiu, Is.True);
            Assert.That(burning, Is.True);
            Assert.That(tags, Is.EqualTo(new[] { fireFlame, slowed }));
        }

        [Test]
        public void GameplayTagContainer_IgnoresRepeatedAndInvalid()
        {
            var fireFlame = new GameplayTag(fireFlameDefinition);

            var tags = new GameplayTagContainer(fireFlame, fireFlame, default);

            Assert.That(tags, Is.EqualTo(new[] { fireFlame }));
        }

        [Test]
        public void GameplayTagQuery_AllAnyNone()
        {
            var stunned = new GameplayTag(stunnedDefinition);
            var fire = new GameplayTag(fireDefinition);
            var ice = new GameplayTag(iceDefinition);
            var immune = new GameplayTag(immuneDefinition);

            // --8<-- [start:query]

            var query = new GameplayTagQuery();
            query.All.Add(stunned);
            query.Any.Add(fire);
            query.Any.Add(ice);
            query.None.Add(immune);

            bool atordoadoComGelo = query.Matches(new GameplayTagContainer(stunned, ice));        // true
            bool imune = query.Matches(new GameplayTagContainer(stunned, ice, immune));           // false: tem Imune
            bool semElemento = query.Matches(new GameplayTagContainer(stunned));                  // false: falta fogo ou gelo
            // --8<-- [end:query]

            Assert.That(atordoadoComGelo, Is.True);
            Assert.That(imune, Is.False);
            Assert.That(semElemento, Is.False);
        }

        [Test]
        public void Validate_ForwardsToQuery_WithFieldName()
        {
            var stunned = new GameplayTag(stunnedDefinition);
            var owner = ScriptableObject.CreateInstance<AreaDeEfeito>();
            owner.Alvos.Add(stunned);
            owner.Condicao.All.Add(stunned);
            owner.Condicao.None.Add(stunned);
            var result = new ValidationResult(owner);

            owner.Validate(result);

            Object.DestroyImmediate(owner);
            Assert.That(result.Messages, Has.Exactly(1).Matches<ValidationMessage>(m =>
                m.Severity == ValidationSeverity.Error && m.PropertyPath == "condicao"));
            Assert.That(result.Messages, Has.Count.EqualTo(1));
        }

        [Test]
        public void Validate_ForwardsToContainer_WithFieldName()
        {
            var owner = ScriptableObject.CreateInstance<AreaDeEfeito>();
            owner.Condicao.Any.Add(new GameplayTag(fireDefinition));
            using (var serialized = new SerializedObject(owner))
            {
                serialized.FindProperty("alvos.tags").arraySize = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var result = new ValidationResult(owner);

            owner.Validate(result);

            Object.DestroyImmediate(owner);
            Assert.That(result.Messages, Has.Exactly(1).Matches<ValidationMessage>(m =>
                m.Severity == ValidationSeverity.Error && m.PropertyPath == "alvos.tags.Array.data[0]"));
            Assert.That(result.Messages, Has.Count.EqualTo(1));
        }

        [Test]
        public void TagDebug_LogChain_LogsNameAndParents()
        {
            var slowed = new GameplayTag(slowedDefinition);
            LogAssert.Expect(LogType.Log, "State.Debuff.Slowed → State.Debuff → State");

            TagDebug.LogChain(slowed);

            LogAssert.NoUnexpectedReceived();
        }
    }

    /// <summary>Dono dos campos de tag do exemplo de validação, que repassa a chamada a cada campo.</summary>
    public sealed class AreaDeEfeito : ScriptableObject, IValidatable
    {
        [SerializeField] private GameplayTagContainer alvos = new GameplayTagContainer();
        [SerializeField] private GameplayTagQuery condicao = new GameplayTagQuery();

        /// <summary>Tags dos alvos.</summary>
        public GameplayTagContainer Alvos => alvos;

        /// <summary>Condição de tags.</summary>
        public GameplayTagQuery Condicao => condicao;

        // --8<-- [start:validate]
        public void Validate(ValidationResult result)
        {
            alvos.Validate(result, nameof(alvos));        // GameplayTagContainer
            condicao.Validate(result, nameof(condicao));  // GameplayTagQuery
        }
        // --8<-- [end:validate]
    }

    // --8<-- [start:tag-debug]

    public static class TagDebug
    {
        public static void LogChain(GameplayTag tag)
        {
            var text = new StringBuilder(tag.Name);
            for (GameplayTagDefinition parent = tag.GetDefinition()?.Parent; parent != null; parent = parent.Parent)
            {
                text.Append(" → ").Append(parent.name);
            }

            Debug.Log(text.ToString());   // State.Debuff.Slowed → State.Debuff → State
        }
    }
    // --8<-- [end:tag-debug]
}
