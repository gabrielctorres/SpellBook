using System.Globalization;
using SpellBook.Core;
using SpellBook.Events;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Samples.Sandbox
{
    /// <summary>
    /// Cria o conjurador e o boneco e cuida das teclas da cena: pausa do grupo de gameplay, câmera lenta pelo
    /// <c>Time.timeScale</c> e reinício. Não depende do grupo pausável, então as teclas valem durante a pausa.
    /// No início e no reinício, liga o Console sem HUD só quando a âncora do HUD está vazia.
    /// </summary>
    [HelpURL(DocUrls.Samples + "#sandboxdirector")]
    public sealed class SandboxDirector : MonoBehaviour
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        [SerializeField, Tooltip("Prefab do conjurador.")]
        private GameObject casterPrefab;

        [SerializeField, Tooltip("Prefab do boneco de treino.")]
        private GameObject dummyPrefab;

        [SerializeField, Tooltip("Onde o conjurador aparece.")]
        private Transform casterSpawn;

        [SerializeField, Tooltip("Onde o boneco aparece.")]
        private Transform dummySpawn;

        [SerializeField, Tooltip("Grupo de tempo do gameplay, pausado pela tecla de pausa.")]
        private PausableTimeSource gameplayTime;

        [SerializeField, Tooltip("Escala de tempo da câmera lenta.")]
        private float slowScale = 0.25f;

        [SerializeField, Tooltip("Pausa ou retoma o grupo de gameplay.")]
        private KeyCode pauseKey = KeyCode.P;

        [SerializeField, Tooltip("Liga ou desliga a câmera lenta.")]
        private KeyCode slowKey = KeyCode.T;

        [SerializeField, Tooltip("Reinicia a cena.")]
        private KeyCode restartKey = KeyCode.R;

        [SerializeField, Tooltip("Âncora em que o HUD se publica. Vazia ou sem âncora, o HUD conta como ausente.")]
        private RuntimeAnchor hudAnchor;

        [SerializeField, Tooltip("Ouvinte que escreve no Console o que o HUD mostraria. Ligado só sem o HUD.")]
        private EventListener consoleWithoutHud;

        private GameObject _caster;
        private GameObject _dummy;

        private void Start()
        {
            UpdateConsoleWithoutHud();
            Spawn();
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
            }

            if (Input.GetKeyDown(slowKey))
            {
                ToggleSlowMotion();
            }

            if (Input.GetKeyDown(restartKey))
            {
                Restart();
            }
        }

        private void OnDestroy()
        {
            UnityEngine.Time.timeScale = 1f;
        }

        private void TogglePause()
        {
            bool paused = !gameplayTime.IsPaused;
            gameplayTime.SetPaused(paused);
            Debug.Log(paused ? "Jogo pausado" : "Jogo retomado", this);
        }

        private void ToggleSlowMotion()
        {
            bool slow = Mathf.Approximately(UnityEngine.Time.timeScale, 1f);
            UnityEngine.Time.timeScale = slow ? slowScale : 1f;
            Debug.Log(slow
                ? string.Format(PtBr, "Câmera lenta: escala {0:0.##}", slowScale)
                : "Velocidade normal: escala 1", this);
        }

        private void Restart()
        {
            gameplayTime.Resume();
            UnityEngine.Time.timeScale = 1f;
            Destroy(_caster);
            Destroy(_dummy);
            UpdateConsoleWithoutHud();
            Spawn();
            Debug.Log("Cena reiniciada", this);
        }

        // Lido só no Start e no reinício: os OnEnable da cena, onde o HUD se publica, rodam antes de qualquer Start.
        private void UpdateConsoleWithoutHud()
        {
            if (consoleWithoutHud != null)
            {
                consoleWithoutHud.enabled = hudAnchor == null || hudAnchor.UntypedValue == null;
            }
        }

        private void Spawn()
        {
            _caster = Instantiate(casterPrefab, casterSpawn.position, casterSpawn.rotation);
            _dummy = Instantiate(dummyPrefab, dummySpawn.position, dummySpawn.rotation);
        }
    }
}
