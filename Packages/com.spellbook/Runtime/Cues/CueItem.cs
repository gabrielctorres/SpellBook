using System;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>
    /// Base de um item de <see cref="CueDefinition"/>: o que um cue toca (VFX, som ou um item próprio). Não guarda estado
    /// de execução; o roteador rastreia o que o item cria.
    /// </summary>
    [Serializable]
    public abstract class CueItem
    {
        /// <summary>
        /// Toca o item com os <paramref name="parameters"/>. O que fica no mundo é criado por
        /// <see cref="CuePlayback.Spawn"/>, para o roteador rastrear e devolver.
        /// </summary>
        public abstract void Play(in CueParameters parameters, CuePlayback playback);

        /// <summary>
        /// Acusa a configuração inválida do item numa definição do <paramref name="kind"/> dado. O
        /// <paramref name="path"/> é o caminho serializado do item, para a mensagem apontar o campo. O padrão não acusa
        /// nada.
        /// </summary>
        public virtual void Validate(ValidationResult result, string path, CueKind kind) { }
    }

    /// <summary>Onde o objeto do cue fica em relação ao alvo. O roteador copia a pose; nunca muda o parent.</summary>
    public enum CuePlacement
    {
        /// <summary>Fica parado onde nasceu.</summary>
        World,

        /// <summary>Copia posição e rotação do alvo, mais o deslocamento, a cada tick.</summary>
        AttachToTarget,

        /// <summary>Copia só a posição do alvo, mais o deslocamento, a cada tick.</summary>
        FollowTarget,
    }

    /// <summary>Quando o roteador devolve o objeto do cue.</summary>
    public enum CueEnd
    {
        /// <summary>Quando as partículas acabam.</summary>
        ParticlesStop,

        /// <summary>Depois de uma duração fixa, pela fonte de tempo do roteador.</summary>
        FixedDuration,

        /// <summary>Quando o cue é removido.</summary>
        OnRemove,

        /// <summary>Quando o clipe de áudio acaba.</summary>
        ClipEnd,
    }

    /// <summary>
    /// Dados de uma execução de cue: onde, em quem, por quem e com que intensidade. Crie com <see cref="At"/> ou
    /// <see cref="On"/>; cada <c>With…</c> devolve uma cópia.
    /// </summary>
    public readonly struct CueParameters
    {
        /// <summary>Posição no mundo.</summary>
        public Vector3 Position { get; }

        /// <summary>Rotação no mundo.</summary>
        public Quaternion Rotation { get; }

        /// <summary>Verdadeiro se <see cref="Normal"/> foi informada.</summary>
        public bool HasNormal { get; }

        /// <summary>Normal da superfície, como a de um impacto. Só vale com <see cref="HasNormal"/>.</summary>
        public Vector3 Normal { get; }

        /// <summary>Quem recebe o cue, ou <c>null</c>.</summary>
        public Transform Target { get; }

        /// <summary>Quem causou o cue, ou <c>null</c>.</summary>
        public Transform Instigator { get; }

        /// <summary>Verdadeiro se a magnitude foi informada. Sem ela, vale a magnitude padrão da definição.</summary>
        public bool HasMagnitude { get; }

        /// <summary>Magnitude bruta, como o dano causado. Só vale com <see cref="HasMagnitude"/>.</summary>
        public float Magnitude { get; }

        /// <summary>Magnitude de 0 a 1. Só vale com <see cref="HasMagnitude"/>.</summary>
        public float NormalizedMagnitude { get; }

        /// <summary>Valor livre para itens próprios.</summary>
        public float UserFloat { get; }

        /// <summary>Valor livre para itens próprios.</summary>
        public int UserInt { get; }

        private CueParameters(
            Vector3 position, Quaternion rotation, bool hasNormal, Vector3 normal, Transform target, Transform instigator,
            bool hasMagnitude, float magnitude, float normalizedMagnitude, float userFloat, int userInt)
        {
            Position = position;
            Rotation = rotation;
            HasNormal = hasNormal;
            Normal = normal;
            Target = target;
            Instigator = instigator;
            HasMagnitude = hasMagnitude;
            Magnitude = magnitude;
            NormalizedMagnitude = normalizedMagnitude;
            UserFloat = userFloat;
            UserInt = userInt;
        }

        /// <summary>Cue num ponto do mundo, sem rotação.</summary>
        public static CueParameters At(Vector3 position)
        {
            return new CueParameters(position, Quaternion.identity, false, Vector3.zero, null, null, false, 0f, 0f, 0f, 0);
        }

        /// <summary>Cue no alvo, com a posição e a rotação dele.</summary>
        public static CueParameters On(Transform target)
        {
            return new CueParameters(target.position, target.rotation, false, Vector3.zero, target, null, false, 0f, 0f, 0f, 0);
        }

        /// <summary>Cópia com outra rotação.</summary>
        public CueParameters WithRotation(Quaternion rotation) =>
            new CueParameters(Position, rotation, HasNormal, Normal, Target, Instigator, HasMagnitude, Magnitude,
                NormalizedMagnitude, UserFloat, UserInt);

        /// <summary>Cópia com a normal informada.</summary>
        public CueParameters WithNormal(Vector3 normal) =>
            new CueParameters(Position, Rotation, true, normal, Target, Instigator, HasMagnitude, Magnitude,
                NormalizedMagnitude, UserFloat, UserInt);

        /// <summary>Cópia com outro alvo. A posição não muda.</summary>
        public CueParameters WithTarget(Transform target) =>
            new CueParameters(Position, Rotation, HasNormal, Normal, target, Instigator, HasMagnitude, Magnitude,
                NormalizedMagnitude, UserFloat, UserInt);

        /// <summary>Cópia com outro causador.</summary>
        public CueParameters WithInstigator(Transform instigator) =>
            new CueParameters(Position, Rotation, HasNormal, Normal, Target, instigator, HasMagnitude, Magnitude,
                NormalizedMagnitude, UserFloat, UserInt);

        /// <summary>Cópia com a magnitude bruta e a normalizada (presa entre 0 e 1).</summary>
        public CueParameters WithMagnitude(float magnitude, float normalizedMagnitude) =>
            new CueParameters(Position, Rotation, HasNormal, Normal, Target, Instigator, true, magnitude,
                Mathf.Clamp01(normalizedMagnitude), UserFloat, UserInt);

        /// <summary>Cópia com outro valor livre.</summary>
        public CueParameters WithUserFloat(float value) =>
            new CueParameters(Position, Rotation, HasNormal, Normal, Target, Instigator, HasMagnitude, Magnitude,
                NormalizedMagnitude, value, UserInt);

        /// <summary>Cópia com outro valor livre.</summary>
        public CueParameters WithUserInt(int value) =>
            new CueParameters(Position, Rotation, HasNormal, Normal, Target, Instigator, HasMagnitude, Magnitude,
                NormalizedMagnitude, UserFloat, value);
    }

    /// <summary>
    /// Referência a um cue ativo no roteador. O padrão é inválido. Quando o slot do cue é reutilizado, os handles antigos
    /// deixam de valer.
    /// </summary>
    public readonly struct CueHandle : IEquatable<CueHandle>
    {
        internal CueHandle(int slot, int generation)
        {
            Slot = slot;
            Generation = generation;
        }

        internal int Slot { get; }

        internal int Generation { get; }

        /// <summary>Falso no handle padrão e no de uma tag sem definição.</summary>
        public bool IsValid => Generation > 0;

        public bool Equals(CueHandle other) => Slot == other.Slot && Generation == other.Generation;

        public override bool Equals(object obj) => obj is CueHandle other && Equals(other);

        public override int GetHashCode() => (Slot * 397) ^ Generation;

        public static bool operator ==(CueHandle a, CueHandle b) => a.Equals(b);

        public static bool operator !=(CueHandle a, CueHandle b) => !a.Equals(b);
    }

    /// <summary>O que um item de cue pede ao roteador para criar: pose, posicionamento, alvo e fim.</summary>
    public readonly struct CueSpawnRequest
    {
        public CueSpawnRequest(
            Vector3 position, Quaternion rotation, Quaternion localRotation, CuePlacement placement, Transform target,
            Vector3 offset, CueEnd end, float duration)
        {
            Position = position;
            Rotation = rotation;
            LocalRotation = localRotation;
            Placement = placement;
            Target = target;
            Offset = offset;
            End = end;
            Duration = duration;
        }

        /// <summary>Posição inicial no mundo.</summary>
        public Vector3 Position { get; }

        /// <summary>Rotação no mundo ao nascer, antes de <see cref="LocalRotation"/>.</summary>
        public Quaternion Rotation { get; }

        /// <summary>
        /// Rotação extra, aplicada sobre <see cref="Rotation"/> ao nascer e, com <see cref="CuePlacement.AttachToTarget"/>,
        /// sobre a do alvo a cada tick. O <c>default</c> (todo zero) vale como <see cref="Quaternion.identity"/>, sem
        /// rotação extra.
        /// </summary>
        public Quaternion LocalRotation { get; }

        /// <summary>Como o objeto acompanha o alvo.</summary>
        public CuePlacement Placement { get; }

        /// <summary>Alvo acompanhado, ou <c>null</c>.</summary>
        public Transform Target { get; }

        /// <summary>Deslocamento em relação ao alvo.</summary>
        public Vector3 Offset { get; }

        /// <summary>Quando o objeto volta.</summary>
        public CueEnd End { get; }

        /// <summary>Segundos até voltar, com <see cref="CueEnd.FixedDuration"/>.</summary>
        public float Duration { get; }
    }
}
