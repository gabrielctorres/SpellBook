using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Fonte do indicador de tag (R9): acende e apaga pela entrada e saída da tag ou pela consulta, por evento (CA4, C4),
    /// prévia acesa e nada ouvido depois de desligar.
    /// </summary>
    public class TagSourceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Tag_OnOffByEvent()
        {
            GameplayTag atordoado = _gas.CreateTag("Atordoado");
            GameplayTag atordoadoForte = _gas.CreateTag("Atordoado.Forte", atordoado);
            GameplayTag outra = _gas.CreateTag("Outra");
            AbilitySystemComponent asc = _gas.CreateAsc();
            Recorder view = CreateIndicator(asc, new TagSource { Mode = TagSourceMode.Tag, Tag = atordoado });
            TagIndicatorModel initial = view.Last;

            asc.AddTag(atordoado);
            bool lit = view.Last.Active;
            asc.AddTag(outra);
            int rendersAfterOther = view.Renders;
            asc.RemoveTag(atordoado);
            bool unlit = view.Last.Active;
            asc.AddTag(atordoadoForte);

            Assert.That(initial.IsEmpty, Is.False);
            Assert.That(initial.Active, Is.False);
            Assert.That(lit, Is.True);
            Assert.That(rendersAfterOther, Is.EqualTo(2), "outra tag não muda o indicador e não deveria redesenhar");
            Assert.That(unlit, Is.False);
            Assert.That(view.Last.Active, Is.True, "a tag filha satisfaz a tag pai");
            Assert.That(view.Renders, Is.EqualTo(4), "um desenho ao ligar e um por mudança, sem driver");
        }

        [Test]
        public void Query_AllAnyNone_ByEvent()
        {
            GameplayTag a = _gas.CreateTag("A");
            GameplayTag b = _gas.CreateTag("B");
            GameplayTag c = _gas.CreateTag("C");
            GameplayTag d = _gas.CreateTag("D");
            AbilitySystemComponent asc = _gas.CreateAsc();
            var source = new TagSource { Mode = TagSourceMode.Query };
            source.Query.All.Add(a);
            source.Query.Any.Add(b);
            source.Query.Any.Add(c);
            source.Query.None.Add(d);
            Recorder view = CreateIndicator(asc, source);

            asc.AddTag(a);
            bool onlyAll = view.Last.Active;
            asc.AddTag(c);
            bool allAndAny = view.Last.Active;
            asc.AddTag(d);
            bool withNone = view.Last.Active;
            asc.RemoveTag(d);

            Assert.That(onlyAll, Is.False, "falta uma das tags de Alguma");
            Assert.That(allAndAny, Is.True);
            Assert.That(withNone, Is.False, "uma tag de Nenhuma apaga");
            Assert.That(view.Last.Active, Is.True);
        }

        [Test]
        public void Preview_On()
        {
            var source = new TagSource { Mode = TagSourceMode.Tag };

            TagIndicatorModel preview = source.CreatePreview(new UIPreviewContext(1));

            Assert.That(preview.IsEmpty, Is.False);
            Assert.That(preview.Active, Is.True);
            Assert.That(source.OwnerType, Is.EqualTo(typeof(AbilitySystemComponent)));
        }

        [Test]
        public void Disabled_StopsListening()
        {
            GameplayTag atordoado = _gas.CreateTag("Atordoado");
            AbilitySystemComponent asc = _gas.CreateAsc();
            Recorder view = CreateIndicator(asc, new TagSource { Mode = TagSourceMode.Tag, Tag = atordoado });

            view.gameObject.SetActive(false);
            int rendersWhenDisabled = view.Renders;
            asc.AddTag(atordoado);

            Assert.That(view.Renders, Is.EqualTo(rendersWhenDisabled), "o binder desligado não deveria ouvir a tag");
        }

        // Indicador ligado ao ASC pelo dono explícito, sem driver: só o OnTagChanged do GAS o atualiza
        private Recorder CreateIndicator(AbilitySystemComponent asc, TagSource source)
        {
            var gameObject = new GameObject("indicador");
            gameObject.SetActive(false);
            _created.Add(gameObject);
            Recorder view = gameObject.AddComponent<Recorder>();
            TagIndicatorBinder binder = gameObject.AddComponent<TagIndicatorBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            binder.SetSource(source);
            binder.SetView(view);
            gameObject.SetActive(true);
            return view;
        }

        /// <summary>View de teste: guarda o último modelo e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<TagIndicatorModel>
        {
            public int Renders;
            public TagIndicatorModel Last;

            public void Render(in TagIndicatorModel model)
            {
                Renders++;
                Last = model;
            }
        }
    }
}
