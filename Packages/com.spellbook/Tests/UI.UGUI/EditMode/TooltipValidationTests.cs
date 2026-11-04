using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.UI;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>
    /// Validação do <see cref="TooltipTrigger"/> e do <see cref="TooltipBinder"/> (Design 6, R14; CA16), pelo mesmo caminho
    /// do Inspector e do validador, com o objeto e o campo de cada mensagem.
    /// </summary>
    public class TooltipValidationTests
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
            foreach (Object item in _created.Where(o => o != null)) Object.DestroyImmediate(item);
            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Trigger_NoSource_IsError()
        {
            TooltipTrigger trigger = ValidTrigger();
            trigger.SetSource(null);

            AssertSingle(trigger, ValidationSeverity.Error, "source", "Sem fonte. Escolha o que o tooltip mostra.");
        }

        [Test]
        public void Trigger_NoBinderAnchor_IsError()
        {
            TooltipTrigger trigger = ValidTrigger();
            trigger.SetBinder(null);

            AssertSingle(trigger, ValidationSeverity.Error, "binder",
                "Sem âncora de binder. Escolha a âncora em que o TooltipBinder da tela se publica.");
        }

        [Test]
        public void Trigger_NegativeDelay_IsWarning()
        {
            TooltipTrigger trigger = ValidTrigger();
            trigger.SetDelay(-0.5f);

            AssertSingle(trigger, ValidationSeverity.Warning, "delay", "Atraso negativo. O tooltip abre na hora; use 0 ou mais.");
        }

        [Test]
        public void Trigger_PointerMode_NoRaycastGraphic_IsWarning()
        {
            const string text = "Modo de ponteiro sem Graphic com Raycast Target neste objeto nem nos filhos. " +
                "O ponteiro não abre o tooltip; ligue o Raycast Target de um Graphic ou use o modo Focus ou Code.";
            TooltipTrigger trigger = ValidTrigger();
            trigger.GetComponent<Image>().raycastTarget = false;

            trigger.SetMode(TooltipOpenMode.Pointer);
            AssertSingle(trigger, ValidationSeverity.Warning, "mode", text);
            trigger.SetMode(TooltipOpenMode.PointerAndFocus);
            AssertSingle(trigger, ValidationSeverity.Warning, "mode", text);

            trigger.SetMode(TooltipOpenMode.Focus);
            Assert.That(Messages(trigger), Is.Empty, "o foco não depende do raycast");
            trigger.SetMode(TooltipOpenMode.Code);
            Assert.That(Messages(trigger), Is.Empty, "a chamada por código não depende do raycast");

            trigger.SetMode(TooltipOpenMode.Pointer);
            var child = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            _created.Add(child);
            child.transform.SetParent(trigger.transform, false);
            Assert.That(Messages(trigger), Is.Empty, "um Graphic num filho também recebe o ponteiro");
        }

        [Test]
        public void Trigger_SourceRulesUnderSourcePath()
        {
            TooltipTrigger trigger = ValidTrigger();
            trigger.SetSource(new AbilityTooltipSource());

            AssertSingle(trigger, ValidationSeverity.Error, "source.ability",
                "Fonte sem habilidade e sem UIItemContext no objeto. Escolha a habilidade ou use o tooltip num item de lista.");
        }

        [Test]
        public void Trigger_Valid_HasNoMessages()
        {
            Assert.That(Messages(ValidTrigger()), Is.Empty);
        }

        [Test]
        public void Binder_NoView_IsError()
        {
            TooltipBinder binder = Binder();

            AssertSingle(binder, ValidationSeverity.Error, "view", "Sem view. Ligue o componente que desenha o tooltip.");
        }

        [Test]
        public void Binder_ViewNotTooltipView_IsError()
        {
            TooltipBinder binder = Binder();
            binder.SetView(binder.gameObject.AddComponent<Image>());

            AssertSingle(binder, ValidationSeverity.Error, "view",
                "A view Image não implementa IView<TooltipModel>. Ligue um componente que desenhe esse modelo.");

            binder.SetView(binder.gameObject.AddComponent<TooltipView>());
            Assert.That(Messages(binder), Is.Empty, "a TooltipView desenha o modelo do tooltip");
        }

        // Gatilho sem mensagens: fonte com habilidade, âncora, atraso padrão, modo padrão e Image com raycast
        private TooltipTrigger ValidTrigger()
        {
            var item = new GameObject("Item", typeof(RectTransform), typeof(Image));
            _created.Add(item);
            var trigger = item.AddComponent<TooltipTrigger>();
            var anchor = ScriptableObject.CreateInstance<TooltipBinderAnchor>();
            _created.Add(anchor);
            trigger.SetSource(new AbilityTooltipSource { Ability = _gas.CreateAbility() });
            trigger.SetBinder(anchor);
            return trigger;
        }

        private TooltipBinder Binder()
        {
            var host = new GameObject("Tooltip", typeof(RectTransform));
            _created.Add(host);
            return host.AddComponent<TooltipBinder>();
        }

        private static IReadOnlyList<ValidationMessage> Messages(Object target) =>
            SpellBookEditor.CollectMessages(new[] { target });

        // Uma mensagem só, com o objeto clicável (o próprio componente) e o campo a corrigir
        private static void AssertSingle(Object target, ValidationSeverity severity, string propertyPath, string text)
        {
            IReadOnlyList<ValidationMessage> messages = Messages(target);
            Assert.That(messages.Select(m => m.Message), Is.EqualTo(new[] { text }));
            Assert.That(messages[0].Severity, Is.EqualTo(severity));
            Assert.That(messages[0].PropertyPath, Is.EqualTo(propertyPath));
            Assert.That(messages[0].Context, Is.SameAs(target), "a mensagem leva ao objeto validado");
        }
    }
}
