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
        [SerializeField, Tooltip("Caster prefab.")]
        private GameObject casterPrefab;

        [SerializeField, Tooltip("Training dummy prefab.")]
        private GameObject dummyPrefab;

        [SerializeField, Tooltip("Where the caster spawns.")]
        private Transform casterSpawn;

        [SerializeField, Tooltip("Where the dummy spawns.")]
        private Transform dummySpawn;

        [SerializeField, Tooltip("Gameplay time group, paused by the pause key.")]
        private PausableTimeSource gameplayTime;

        [SerializeField, Tooltip("Time scale of the slow motion.")]
        private float slowScale = 0.25f;

        [SerializeField, Tooltip("Pauses or resumes the gameplay group.")]
        private KeyCode pauseKey = KeyCode.P;

        [SerializeField, Tooltip("Turns slow motion on or off.")]
        private KeyCode slowKey = KeyCode.T;

        [SerializeField, Tooltip("Restarts the scene.")]
        private KeyCode restartKey = KeyCode.R;

        [SerializeField, Tooltip("Anchor the HUD publishes itself to. If empty or unset, the HUD counts as absent.")]
        private RuntimeAnchor hudAnchor;

        [SerializeField, Tooltip("Listener that writes to the Console what the HUD would show. Enabled only without the HUD.")]
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
            Debug.Log(paused ? "Game paused" : "Game resumed", this);
        }

        private void ToggleSlowMotion()
        {
            bool slow = Mathf.Approximately(UnityEngine.Time.timeScale, 1f);
            UnityEngine.Time.timeScale = slow ? slowScale : 1f;
            Debug.Log(slow
                ? string.Format(CultureInfo.InvariantCulture, "Slow motion: scale {0:0.##}", slowScale)
                : "Normal speed: scale 1", this);
        }

        private void Restart()
        {
            gameplayTime.Resume();
            UnityEngine.Time.timeScale = 1f;
            Destroy(_caster);
            Destroy(_dummy);
            UpdateConsoleWithoutHud();
            Spawn();
            Debug.Log("Scene restarted", this);
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
