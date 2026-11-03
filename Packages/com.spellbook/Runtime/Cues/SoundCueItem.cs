using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;
using UnityEngine.Audio;

namespace SpellBook.Cues
{
    /// <summary>
    /// Item de som: toca o clipe num objeto do prefab de áudio do roteador, criado pelo spawner, com volume e altura
    /// sorteados nas faixas. O roteador devolve o objeto quando o clipe acaba, ou no <c>Remove</c> com loop.
    /// </summary>
    [Serializable, TypeCategory("Apresentação", Summary = "Toca um clipe num objeto de áudio do spawner e o devolve no fim do clipe.",
        Line = "Som {Clip}{Loop? · loop}")]
    public sealed class SoundCueItem : CueItem
    {
        [Tooltip("Clipe tocado.")]
        public AudioClip Clip;

        [Tooltip("Faixa do volume, de 0 a 1: um valor sorteado a cada vez.")]
        public Vector2 Volume = Vector2.one;

        [Tooltip("Faixa da altura (pitch): um valor sorteado a cada vez.")]
        public Vector2 Pitch = Vector2.one;

        [Range(0f, 1f), Tooltip("0 é som 2D; 1 é som 3D, pela posição do cue.")]
        public float SpatialBlend;

        [Tooltip("Grupo do mixer. Vazio usa a saída padrão.")]
        public AudioMixerGroup Mixer;

        [Tooltip("Repete até o cue ser removido. Só num cue Looping.")]
        public bool Loop;

        [Tooltip("Multiplica o volume sorteado pela magnitude normalizada do cue.")]
        public bool VolumeByMagnitude;

        public override void Play(in CueParameters parameters, CuePlayback playback)
        {
            if (Clip == null) return;

            // Sempre os dois sorteios, na mesma ordem: a mesma semente dá a mesma sequência
            IRandomSource random = playback.Random;
            float volume = Mathf.Lerp(Volume.x, Volume.y, random.Value());
            float pitch = Mathf.Lerp(Pitch.x, Pitch.y, random.Value());
            if (VolumeByMagnitude) volume *= playback.NormalizedMagnitude(parameters);

            // Na altura 0 o clipe não anda e nunca acabaria
            if (Mathf.Approximately(pitch, 0f)) return;

            GameObject prefab = playback.AudioPrefab;
            if (prefab == null) return;

            bool loop = Loop && playback.Cue.Kind == CueKind.Looping;
            GameObject instance = playback.Spawn(prefab, null, new CueSpawnRequest(
                parameters.Position, parameters.Rotation, Quaternion.identity, CuePlacement.World, null, Vector3.zero,
                loop ? CueEnd.OnRemove : CueEnd.ClipEnd, Clip.length / Mathf.Abs(pitch)));
            if (instance == null) return;

            if (instance.TryGetComponent(out CueAudioPlayer player))
            {
                player.Play(Clip, volume, pitch, SpatialBlend, Mixer, loop);
            }
        }

        public override void Validate(ValidationResult result, string path, CueKind kind)
        {
            if (Clip == null)
            {
                result.AddError("Som sem clipe. Escolha o clipe tocado.", path + "." + nameof(Clip));
            }

            if (Volume.x > Volume.y)
            {
                result.AddError("Faixa de volume invertida: o mínimo é maior que o máximo.", path + "." + nameof(Volume));
            }

            if (Volume.x < 0f || Volume.y < 0f || Volume.x > 1f || Volume.y > 1f)
            {
                result.AddError("Volume fora de 0 a 1.", path + "." + nameof(Volume));
            }

            if (Pitch.x > Pitch.y)
            {
                result.AddError("Faixa de altura invertida: o mínimo é maior que o máximo.", path + "." + nameof(Pitch));
            }

            if (Loop && kind == CueKind.Burst)
            {
                result.AddError("Loop num cue Burst não repete. Desligue o loop ou mude o cue para Looping.",
                    path + "." + nameof(Loop));
            }
        }
    }
}
