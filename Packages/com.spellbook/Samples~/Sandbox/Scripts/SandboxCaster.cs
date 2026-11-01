using System.Globalization;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.Samples.Sandbox
{
    /// <summary>
    /// Mira com o mouse num plano horizontal e conjura a habilidade com uma tecla. Escreve no Console quando a
    /// habilidade é conjurada e o motivo de cada falha (recarga, mana).
    /// </summary>
    [RequireComponent(typeof(AbilitySystemComponent))]
    [HelpURL(DocUrls.Samples + "#sandboxcaster")]
    public sealed class SandboxCaster : MonoBehaviour
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        [SerializeField, Tooltip("Habilidade conjurada pela tecla.")]
        private GameplayAbility ability;

        [SerializeField, Tooltip("Tecla que conjura a habilidade.")]
        private KeyCode castKey = KeyCode.Alpha1;

        [SerializeField, Tooltip("Altura do plano de mira, em metros.")]
        private float aimHeight = 1.2f;

        private AbilitySystemComponent _asc;
        private Camera _camera;

        private void Awake()
        {
            _asc = GetComponent<AbilitySystemComponent>();
            _camera = Camera.main;
        }

        private void OnEnable()
        {
            _asc.OnAbilityActivated += HandleActivated;
            _asc.OnAbilityFailed += HandleFailed;
        }

        private void OnDisable()
        {
            _asc.OnAbilityActivated -= HandleActivated;
            _asc.OnAbilityFailed -= HandleFailed;
        }

        private void Update()
        {
            if (_camera == null || !TryGetAimPoint(out Vector3 point))
            {
                return;
            }

            Vector3 toPoint = point - transform.position;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toPoint);
            }

            if (Input.GetKeyDown(castKey))
            {
                _asc.TryActivateAbility(ability, point);
            }
        }

        /// <summary>Ponto do plano <c>y = aimHeight</c> sob o mouse. Usa um plano matemático, sem collider.</summary>
        private bool TryGetAimPoint(out Vector3 point)
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, aimHeight, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private void HandleActivated(AbilitySpec spec)
        {
            if (spec.Ability == ability)
            {
                Debug.Log("Bola de Fogo conjurada", this);
            }
        }

        private void HandleFailed(GameplayAbility failed, AbilityActivationResult result)
        {
            if (failed != ability)
            {
                return;
            }

            switch (result)
            {
                case AbilityActivationResult.OnCooldown:
                    float remaining = _asc.GetCooldownTimeRemaining(ability.CooldownEffect);
                    Debug.Log(string.Format(PtBr, "Bola de Fogo em recarga: {0:0.0} s", remaining), this);
                    break;
                case AbilityActivationResult.CannotAffordCost:
                    Debug.Log("Sem mana para a Bola de Fogo", this);
                    break;
                default:
                    Debug.Log($"Bola de Fogo não disparou: {result}", this);
                    break;
            }
        }
    }
}
