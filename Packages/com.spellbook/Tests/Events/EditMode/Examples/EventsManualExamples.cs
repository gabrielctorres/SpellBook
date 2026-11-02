// --8<-- [start:using-system]
using System;
// --8<-- [end:using-system]
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
// --8<-- [start:using-events]
using SpellBook.Events;
// --8<-- [end:using-events]
// --8<-- [start:using-unityengine]
using UnityEngine;
// --8<-- [end:using-unityengine]
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests.Examples
{
    /// <summary>
    /// Exemplos da página <c>modulos/eventos.md</c> do manual. Cada trecho entre os marcadores <c>--8&lt;--</c> aparece
    /// na página, e os testes conferem o que o texto em volta diz dele (guia do manual, parte 9).
    /// </summary>
    public class EventsManualExamples
    {
        [Test]
        public void PartidaComecou_IsStructEvent()
        {
            Type type = typeof(PartidaComecou);

            Assert.That(type.IsValueType, Is.True);
            Assert.That(typeof(IEvent).IsAssignableFrom(type), Is.True);
        }

        [Test]
        public void Placar_SubscribeThenRaise_DeliversFase()
        {
            int fase = 0;
            Action<PartidaComecou> aoComecar = evento => fase = evento.Fase;
            EventBus<PartidaComecou>.Subscribe(aoComecar);

            try
            {
                // --8<-- [start:placar-raise]

                // Em qualquer outro sistema:
                EventBus<PartidaComecou>.Raise(new PartidaComecou { Fase = 2 });
                // --8<-- [end:placar-raise]
            }
            finally
            {
                EventBus<PartidaComecou>.Unsubscribe(aoComecar);
            }

            Assert.That(fase, Is.EqualTo(2));
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(Placar)), Is.True);
        }

        [Test]
        public void BarraDeVida_RaisedThenRaise_DeliversVida()
        {
            var vidaMudou = ScriptableObject.CreateInstance<IntEventChannel>();
            int vida = 0;
            Action<int> aoMudar = valor => vida = valor;
            vidaMudou.Raised += aoMudar;

            // --8<-- [start:barra-de-vida-raise]

            // Em quem dispara, com o mesmo asset no campo:
            vidaMudou.Raise(80);
            // --8<-- [end:barra-de-vida-raise]

            vidaMudou.Raised -= aoMudar;
            int lastPayload = vidaMudou.LastPayload;
            Object.DestroyImmediate(vidaMudou);
            Assert.That(vida, Is.EqualTo(80));
            Assert.That(lastPayload, Is.EqualTo(80));
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(BarraDeVida)), Is.True);
        }

        [Test]
        public void TocarSom_IsOfferedByReactionPicker()
        {
            Type[] candidates = TypePickerCatalog.GetCandidates(typeof(Reaction)).Select(e => e.Type).ToArray();

            Assert.That(candidates, Does.Contain(typeof(TocarSom)));
        }

        [Test]
        public void DanoEventChannel_RaiseDeliversPayload_AndResponseIsOfferedByPicker()
        {
            var danoRecebido = ScriptableObject.CreateInstance<DanoEventChannel>();
            Dano recebido = default;
            Action<Dano> aoReceber = dano => recebido = dano;
            danoRecebido.Raised += aoReceber;

            danoRecebido.Raise(new Dano { Valor = 10, Ponto = Vector3.up });

            danoRecebido.Raised -= aoReceber;
            Type payload = danoRecebido.PayloadType;
            Object.DestroyImmediate(danoRecebido);
            Type[] responses = TypePickerCatalog.GetCandidates(typeof(EventResponse)).Select(e => e.Type).ToArray();
            Assert.That(recebido.Valor, Is.EqualTo(10));
            Assert.That(recebido.Ponto, Is.EqualTo(Vector3.up));
            Assert.That(payload, Is.EqualTo(typeof(Dano)));
            Assert.That(responses, Does.Contain(typeof(DanoEventResponse)));
        }
    }

    // --8<-- [start:partida-comecou]

    public struct PartidaComecou : IEvent
    {
        public int Fase;
    }
    // --8<-- [end:partida-comecou]

    // --8<-- [start:placar]

    public sealed class Placar : MonoBehaviour
    {
        private void OnEnable()  { EventBus<PartidaComecou>.Subscribe(AoComecar); }
        private void OnDisable() { EventBus<PartidaComecou>.Unsubscribe(AoComecar); }

        private void AoComecar(PartidaComecou evento)
        {
            Debug.Log($"Fase {evento.Fase}");
        }
    }
    // --8<-- [end:placar]

    // --8<-- [start:barra-de-vida]

    public sealed class BarraDeVida : MonoBehaviour
    {
        [SerializeField] private IntEventChannel vidaMudou;

        private void OnEnable()  { vidaMudou.Raised += AoMudar; }
        private void OnDisable() { vidaMudou.Raised -= AoMudar; }

        private void AoMudar(int vida)
        {
            Debug.Log($"Vida: {vida}");
        }
    }
    // --8<-- [end:barra-de-vida]

    // --8<-- [start:tocar-som]

    [Serializable]
    public sealed class TocarSom : Reaction
    {
        [SerializeField] private AudioSource fonte;

        public override void Execute(EventListener listener)
        {
            fonte.Play();
        }
    }
    // --8<-- [end:tocar-som]

    // --8<-- [start:dano]

    [Serializable]
    public struct Dano
    {
        public int Valor;
        public Vector3 Ponto;
    }

    [CreateAssetMenu(menuName = "Jogo/Events/Dano Channel")]
    public sealed class DanoEventChannel : EventChannel<Dano>
    {
    }

    [Serializable]
    public sealed class DanoEventResponse : EventResponse<Dano>
    {
    }
    // --8<-- [end:dano]
}
