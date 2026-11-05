using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Estado do gerenciador do GAS sem UI (ME8, CA7, CA8 e CA15): grupos da lista, rótulos da barra e dos chips,
    /// estados vazios do DS 7, resumo de tags, árvore de tags e caminhos do seletor de tag.
    /// </summary>
    public class GasManagerModelTests
    {
        private GasFactory _factory;
        private GameplayTagDefinition _ability;
        private GameplayTagDefinition _fireball;
        private GameplayTagDefinition _burning;
        private GameplayAbility _gaFireball;
        private GameplayEffect _geBurn;

        [SetUp]
        public void SetUp()
        {
            _factory = new GasFactory();
            GameplayTag ability = _factory.CreateTag("Ability");
            GameplayTag fireball = _factory.CreateTag("Ability.Fireball", ability);
            GameplayTag burning = _factory.CreateTag("Status.Burning");
            _ability = ability.GetDefinition();
            _fireball = fireball.GetDefinition();
            _burning = burning.GetDefinition();
            _gaFireball = _factory.CreateAbility(fireball);
            _gaFireball.name = "GA_Fireball";
            _geBurn = _factory.CreateDurationEffect(3f, burning, fireball, ability);
            _geBurn.name = "GE_Burn";
        }

        [TearDown]
        public void TearDown() => _factory.Dispose();

        private GasManagerModel CreateModel(params Object[] assets)
        {
            var model = new GasManagerModel();
            model.SetIndex(GasIndex.Build(assets));
            return model;
        }

        private GasManagerModel CreateFullModel() => CreateModel(_gaFireball, _geBurn, _ability, _fireball, _burning);

        private static string[] Describe(GasManagerModel model) =>
            model.Rows.Select(r => r.IsHeader ? $"[{GasManagerModel.KindName(r.Kind)}]" : r.Entry.Name).ToArray();

        [Test]
        public void Rows_AllKinds_HeaderBeforeEachGroup()
        {
            GasManagerModel model = CreateFullModel();

            Assert.That(Describe(model), Is.EqualTo(new[]
            {
                "[Habilidades]", "GA_Fireball", "[Efeitos]", "GE_Burn", "[Tags]", "Ability", "Ability.Fireball",
                "Status.Burning"
            }));
        }

        [Test]
        public void Rows_KindFilter_NoHeaders()
        {
            GasManagerModel model = CreateFullModel();

            model.SetKind(GasIndexKind.Tag);
            model.SetText("fire");

            Assert.That(Describe(model), Is.EqualTo(new[] { "Ability.Fireball" }));
        }

        [Test]
        public void Labels_KindCountAndChips()
        {
            GasManagerModel model = CreateFullModel();

            Assert.That(model.KindLabel(GasIndexKind.Tag), Is.EqualTo("Tags 3"));
            Assert.That(model.KindLabel(GasIndexKind.Attribute), Is.EqualTo("Atributos 0"));
            Assert.That(GasManagerModel.KindName(GasIndexKind.Ability), Is.EqualTo("Habilidades"));
            Assert.That(GasManagerModel.TagChip(_ability), Is.EqualTo("Ability e filhas"));
        }

        [Test]
        public void ClearFilters_ShowsAll()
        {
            GasManagerModel model = CreateFullModel();
            model.SetKind(GasIndexKind.Effect);
            model.SetTag(_ability);
            model.SetText("x");
            Assert.That(model.HasFilters, Is.True);

            model.ClearFilters();

            Assert.That(model.HasFilters, Is.False);
            Assert.That(model.Rows.Count(r => !r.IsHeader), Is.EqualTo(5));
        }

        [Test]
        public void EmptyMessage_NoAssetsNoResultsOrNone()
        {
            Assert.That(CreateModel().EmptyMessage, Is.EqualTo("Nenhuma habilidade ou efeito no projeto."));

            GasManagerModel model = CreateFullModel();
            Assert.That(model.EmptyMessage, Is.Null);

            model.SetText("nada-assim");
            Assert.That(model.EmptyMessage, Is.EqualTo("Nada com estes filtros."));
        }

        [Test]
        public void TagSummary_TwoTagsAndRest()
        {
            GasManagerModel model = CreateFullModel();
            GasIndexEntry burn = model.Rows.First(r => !r.IsHeader && r.Entry.Asset == _geBurn).Entry;
            GasIndexEntry ability = model.Rows.First(r => !r.IsHeader && r.Entry.Asset == _gaFireball).Entry;
            GasIndexEntry tag = model.Rows.First(r => !r.IsHeader && r.Entry.Asset == _ability).Entry;

            Assert.That(GasManagerModel.TagSummary(burn), Is.EqualTo("Status.Burning, Ability.Fireball · +1"));
            Assert.That(GasManagerModel.TagSummary(ability), Is.EqualTo("Ability.Fireball"));
            Assert.That(GasManagerModel.TagSummary(tag), Is.Empty);
        }

        [Test]
        public void TagTree_NestsByParent_OrphanIsRoot()
        {
            GasManagerModel model = CreateFullModel();
            model.SetKind(GasIndexKind.Tag);

            var roots = model.TagTree();
            Assert.That(roots.Select(n => n.Entry.Name), Is.EqualTo(new[] { "Ability", "Status.Burning" }));
            Assert.That(roots[0].Children.Select(n => n.Entry.Name), Is.EqualTo(new[] { "Ability.Fireball" }));

            model.SetText("fire");
            Assert.That(model.TagTree().Select(n => n.Entry.Name), Is.EqualTo(new[] { "Ability.Fireball" }));
        }

        [Test]
        public void TagMenu_TagWithChildren_InsideOwnSubmenu()
        {
            GasManagerModel model = CreateFullModel();

            var menu = model.TagMenu();

            Assert.That(menu.Select(m => m.Path), Is.EqualTo(new[]
            {
                "Ability/Ability", "Ability/Ability.Fireball", "Status.Burning"
            }));
            Assert.That(menu.Select(m => m.Tag), Is.EqualTo(new[] { _ability, _fireball, _burning }));
        }
    }
}
