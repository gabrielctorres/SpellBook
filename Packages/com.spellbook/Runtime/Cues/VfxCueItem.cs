using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>
    /// Item de VFX: cria o prefab pelo spawner, posiciona em relação ao alvo, ajusta a escala pela magnitude e toca as
    /// partículas da raiz. O roteador devolve o objeto ao spawner no fim escolhido.
    /// </summary>
    [Serializable, TypeCategory("Apresentação", Summary = "Cria um prefab de VFX no alvo ou no ponto do cue e o devolve ao spawner no fim.")]
    public sealed class VfxCueItem : CueItem
    {
        [Tooltip("Objeto criado a cada vez que o cue toca.")]
        public GameObject Prefab;

        [Tooltip("Cria e devolve o objeto. Vazio usa o spawner padrão do roteador.")]
        public SpawnerAsset Spawner;

        [Tooltip("World fica onde nasceu; AttachToTarget copia posição e rotação do alvo; FollowTarget copia só a posição.")]
        public CuePlacement Placement;

        [Tooltip("Deslocamento em relação ao ponto do cue. Com AttachToTarget, gira com o alvo.")]
        public Vector3 Offset;

        [Tooltip("Rotação, em graus, somada à do cue ao nascer. Com AttachToTarget, somada à do alvo a cada quadro.")]
        public Vector3 Rotation;

        [Tooltip("Multiplica a escala do prefab pela faixa, conforme a magnitude normalizada do cue.")]
        public bool ScaleByMagnitude;

        [Tooltip("Multiplicador da escala na magnitude 0 (x) e na magnitude 1 (y).")]
        public Vector2 ScaleRange = Vector2.one;

        [Tooltip("Quando o objeto volta ao spawner.")]
        public CueEnd End = CueEnd.ParticlesStop;

        [Min(0f), Tooltip("Segundos até voltar, com o fim FixedDuration, pela fonte de tempo do roteador.")]
        public float Duration = 1f;

        public override void Play(in CueParameters parameters, CuePlayback playback)
        {
            if (Prefab == null) return;

            GameObject instance = playback.Spawn(Prefab, Spawner, new CueSpawnRequest(
                parameters.Position, parameters.Rotation, Quaternion.Euler(Rotation), Placement, parameters.Target, Offset,
                End, Duration));
            if (instance == null) return;

            // Sempre a partir da escala do prefab: o objeto do pool pode ter vindo de outra execução
            float factor = ScaleByMagnitude
                ? Mathf.Lerp(ScaleRange.x, ScaleRange.y, playback.NormalizedMagnitude(parameters))
                : 1f;
            instance.transform.localScale = Prefab.transform.localScale * factor;

            if (instance.TryGetComponent(out ParticleSystem particles)) particles.Play(true);
        }
    }
}
