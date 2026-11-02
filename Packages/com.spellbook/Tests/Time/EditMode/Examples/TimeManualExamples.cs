using NUnit.Framework;
using UnityEditor;
// --8<-- [start:using-time]
using SpellBook.Time;
// --8<-- [end:using-time]
// --8<-- [start:using-unityengine]
using UnityEngine;
// --8<-- [end:using-unityengine]
using Object = UnityEngine.Object;

namespace SpellBook.Time.Tests.Examples
{
    /// <summary>
    /// Exemplos da página <c>modulos/tempo.md</c> do manual. Cada trecho entre os marcadores <c>--8&lt;--</c> aparece
    /// na página, e os testes conferem o que o texto em volta diz dele (guia do manual, parte 9).
    /// </summary>
    public class TimeManualExamples
    {
        [Test]
        public void MenuDePausa_AbrirEFechar_PausesAndResumesGroup()
        {
            var gameplay = ScriptableObject.CreateInstance<PausableTimeSource>();
            var objeto = new GameObject("Menu_Test");
            var menu = objeto.AddComponent<MenuDePausa>();
            var serialized = new SerializedObject(menu);
            serialized.FindProperty("gameplay").objectReferenceValue = gameplay;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            menu.AbrirMenu();
            bool pausadoAoAbrir = gameplay.IsPaused;
            float deltaAoAbrir = gameplay.DeltaTime;
            menu.FecharMenu();
            bool pausadoAoFechar = gameplay.IsPaused;

            Object.DestroyImmediate(objeto);
            Object.DestroyImmediate(gameplay);
            Assert.That(pausadoAoAbrir, Is.True);
            Assert.That(deltaAoAbrir, Is.EqualTo(0f));
            Assert.That(pausadoAoFechar, Is.False);
        }

        [Test]
        public void ManualTimeSource_Advance_MatchesCommentedValues()
        {
            // --8<-- [start:manual-criar]
            var tempo = ScriptableObject.CreateInstance<ManualTimeSource>();
            tempo.Advance(1.5f);   // ElapsedTime 1.5, DeltaTime 1.5
            // --8<-- [end:manual-criar]

            try
            {
                Assert.That(tempo.ElapsedTime, Is.EqualTo(1.5d));
                Assert.That(tempo.DeltaTime, Is.EqualTo(1.5f));

                // --8<-- [start:manual-avancar]
                tempo.Advance(0.5f);   // ElapsedTime 2.0, DeltaTime 0.5
                // --8<-- [end:manual-avancar]

                Assert.That(tempo.ElapsedTime, Is.EqualTo(2.0d));
                Assert.That(tempo.DeltaTime, Is.EqualTo(0.5f));
            }
            finally
            {
                // --8<-- [start:manual-destruir]
                // ...
                Object.DestroyImmediate(tempo);   // no TearDown do teste
                // --8<-- [end:manual-destruir]
            }
        }

        [Test]
        public void Cronometro_ConsumesSourceByDifference()
        {
            var manual = ScriptableObject.CreateInstance<ManualTimeSource>();
            ITimeSource fonte = manual;
            manual.Advance(10f);
            double cursor = fonte.ElapsedTime;
            float restante = 3f;

            manual.Advance(0.5f);
            double agora = fonte.ElapsedTime;
            restante -= (float)(agora - cursor);
            cursor = agora;
            float aposAvanco = restante;

            agora = fonte.ElapsedTime;
            restante -= (float)(agora - cursor);

            Object.DestroyImmediate(manual);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(Cronometro)), Is.True);
            Assert.That(aposAvanco, Is.EqualTo(2.5f), "o cursor inicial evita o salto dos 10 s já acumulados");
            Assert.That(restante, Is.EqualTo(2.5f), "fonte parada: a diferença é 0");
        }
    }

    public sealed class MenuDePausa : MonoBehaviour
    {
        // --8<-- [start:menu-de-pausa]
        [SerializeField] private PausableTimeSource gameplay;

        public void AbrirMenu()  { gameplay.Pause(); }
        public void FecharMenu() { gameplay.Resume(); }
        // --8<-- [end:menu-de-pausa]
    }

    // --8<-- [start:cronometro]

    public sealed class Cronometro : MonoBehaviour
    {
        [SerializeField] private TimeSourceAsset timeSource;   // vazio: fonte escalada padrão
        [SerializeField] private float duracao = 3f;

        private ITimeSource _fonte;
        private double _cursor;
        private float _restante;

        private void OnEnable()
        {
            _fonte = timeSource != null ? timeSource : TimeSourceAsset.Scaled;
            _cursor = _fonte.ElapsedTime;   // sem salto no primeiro quadro
            _restante = duracao;
        }

        private void Update()
        {
            double agora = _fonte.ElapsedTime;
            _restante -= (float)(agora - _cursor);
            _cursor = agora;
        }
    }
    // --8<-- [end:cronometro]
}
