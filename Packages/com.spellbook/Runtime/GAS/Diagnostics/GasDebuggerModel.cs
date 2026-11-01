using System;
using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;

namespace SpellBook.GAS.Diagnostics
{
    /// <summary>
    /// Modelo do GAS Debugger (R16 a R18): monta linhas a partir da API pública do ASC, em listas do chamador que são
    /// limpas e reutilizadas, sem alocar depois do aquecimento. As linhas guardam referências (definição, instância, asset,
    /// fonte do modificador); quem desenha lê os nomes. Só lê: não muda o ASC. Fica no runtime para o overlay de debug.
    /// </summary>
    public sealed class GasDebuggerModel
    {
        /// <summary>Log dos eventos do ASC inspecionado. A janela chama <see cref="EventLog.Attach"/> ao trocar de ASC.</summary>
        public EventLog Log { get; } = new EventLog();

        // Object.name aloca a cada leitura: o nome de cada tag é lido uma vez e guardado para a ordenação
        private readonly Dictionary<GameplayTag, string> _tagNames = new Dictionary<GameplayTag, string>();

        /// <summary>
        /// Uma linha por atributo, seguida de uma linha por modificador dele (<see cref="AttributeRow.IsModifier"/>), na
        /// ordem de <paramref name="states"/> e de <see cref="AttributeState.Modifiers"/>.
        /// </summary>
        public void BuildAttributeRows(IReadOnlyList<AttributeState> states, List<AttributeRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            rows.Clear();
            if (states == null) return;

            for (int i = 0; i < states.Count; i++)
            {
                AttributeState state = states[i];
                if (state == null) continue;
                rows.Add(new AttributeRow(state.Definition, state.BaseValue, state.CurrentValue, state.MinValue, state.MaxValue));

                IReadOnlyList<AttributeModifier> modifiers = state.Modifiers;
                for (int m = 0; m < modifiers.Count; m++)
                {
                    AttributeModifier modifier = modifiers[m];
                    rows.Add(new AttributeRow(state.Definition, modifier.Operation, modifier.Value, modifier.Source));
                }
            }
        }

        /// <summary>
        /// Uma linha por tag com contagem, em ordem alfabética (ordinal) do nome da definição, lido na primeira vez que a tag
        /// aparece. Com o
        /// <see cref="AbilitySystemComponent.TagCounts"/> (um <c>Dictionary</c>), não aloca.
        /// </summary>
        public void BuildTagRows(IReadOnlyDictionary<GameplayTag, int> tagCounts, List<TagRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            rows.Clear();
            if (tagCounts == null) return;

            if (tagCounts is Dictionary<GameplayTag, int> dictionary)
            {
                // Enumerador struct: a interface o encaixotaria a cada atualização
                foreach (KeyValuePair<GameplayTag, int> pair in dictionary) rows.Add(new TagRow(pair.Key, pair.Value));
            }
            else
            {
                foreach (KeyValuePair<GameplayTag, int> pair in tagCounts) rows.Add(new TagRow(pair.Key, pair.Value));
            }

            // Inserção: poucas tags e sem o comparador que List.Sort(Comparison) alocaria
            for (int i = 1; i < rows.Count; i++)
            {
                TagRow current = rows[i];
                int j = i - 1;
                while (j >= 0 && string.CompareOrdinal(GetTagName(rows[j].Tag), GetTagName(current.Tag)) > 0)
                {
                    rows[j + 1] = rows[j];
                    j--;
                }
                rows[j + 1] = current;
            }
        }

        private string GetTagName(GameplayTag tag)
        {
            if (!_tagNames.TryGetValue(tag, out string name))
            {
                name = tag.Name;
                _tagNames[tag] = name;
            }
            return name;
        }

        /// <summary>Uma linha por instância, na ordem de <paramref name="effects"/> (a de aplicação, em <see cref="AbilitySystemComponent.ActiveEffects"/>).</summary>
        public void BuildEffectRows(IReadOnlyList<ActiveEffectInstance> effects, List<EffectRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            rows.Clear();
            if (effects == null) return;

            for (int i = 0; i < effects.Count; i++)
            {
                ActiveEffectInstance instance = effects[i];
                if (instance != null) rows.Add(new EffectRow(instance));
            }
        }

        /// <summary>Uma linha por habilidade concedida, na ordem de <see cref="AbilitySystemComponent.Abilities"/>.</summary>
        public void BuildAbilityRows(AbilitySystemComponent asc, List<AbilityRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            rows.Clear();
            if (asc == null) return;

            IReadOnlyList<AbilitySpec> abilities = asc.Abilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                AbilitySpec spec = abilities[i];
                GameplayEffect cooldown = spec.Ability != null ? spec.Ability.CooldownEffect : null;
                float remaining = cooldown != null ? asc.GetCooldownTimeRemaining(cooldown) : 0f;
                float normalized = cooldown != null ? asc.GetCooldownNormalized(cooldown) : 0f;
                rows.Add(new AbilityRow(spec, remaining, normalized));
            }
        }
    }

    /// <summary>
    /// Linha de atributo ou, com <see cref="IsModifier"/>, de um modificador do atributo <see cref="Definition"/>. Os
    /// valores de atributo só valem na linha de atributo; operação, valor e fonte, só na de modificador.
    /// </summary>
    public readonly struct AttributeRow
    {
        public readonly AttributeDefinition Definition;
        public readonly bool IsModifier;
        public readonly float BaseValue;
        public readonly float CurrentValue;
        public readonly float MinValue;
        public readonly float MaxValue;
        public readonly ModifierOperation Operation;
        public readonly float ModifierValue;

        /// <summary>Quem aplicou o modificador: a <see cref="ActiveEffectInstance"/> num modificador de efeito, ou o objeto dado por código.</summary>
        public readonly object Source;

        public AttributeRow(AttributeDefinition definition, float baseValue, float currentValue, float minValue, float maxValue)
        {
            Definition = definition;
            IsModifier = false;
            BaseValue = baseValue;
            CurrentValue = currentValue;
            MinValue = minValue;
            MaxValue = maxValue;
            Operation = default;
            ModifierValue = 0f;
            Source = null;
        }

        public AttributeRow(AttributeDefinition definition, ModifierOperation operation, float modifierValue, object source)
        {
            Definition = definition;
            IsModifier = true;
            BaseValue = 0f;
            CurrentValue = 0f;
            MinValue = 0f;
            MaxValue = 0f;
            Operation = operation;
            ModifierValue = modifierValue;
            Source = source;
        }
    }

    /// <summary>Tag ativa e quantas fontes a concedem.</summary>
    public readonly struct TagRow
    {
        public readonly GameplayTag Tag;
        public readonly int Count;

        public TagRow(GameplayTag tag, int count)
        {
            Tag = tag;
            Count = count;
        }
    }

    /// <summary>Instância de efeito ativa: asset, origem, tempo restante, duração aplicada e pilhas no momento da montagem.</summary>
    public readonly struct EffectRow
    {
        public readonly ActiveEffectInstance Instance;
        public readonly GameplayEffect Effect;
        public readonly AbilitySystemComponent Source;
        public readonly float TimeRemaining;
        public readonly float AppliedDuration;
        public readonly int Stacks;

        public EffectRow(ActiveEffectInstance instance)
        {
            Instance = instance;
            Effect = instance.Effect;
            Source = instance.Source;
            TimeRemaining = instance.TimeRemaining;
            AppliedDuration = instance.AppliedDuration;
            Stacks = instance.CurrentStacks;
        }
    }

    /// <summary>Habilidade concedida: nível, se está ativa e a recarga restante (segundos e de 0 a 1).</summary>
    public readonly struct AbilityRow
    {
        public readonly AbilitySpec Spec;
        public readonly GameplayAbility Ability;
        public readonly int Level;
        public readonly bool IsActive;
        public readonly float CooldownRemaining;
        public readonly float CooldownNormalized;

        public AbilityRow(AbilitySpec spec, float cooldownRemaining, float cooldownNormalized)
        {
            Spec = spec;
            Ability = spec.Ability;
            Level = spec.Level;
            IsActive = spec.IsActive;
            CooldownRemaining = cooldownRemaining;
            CooldownNormalized = cooldownNormalized;
        }
    }

    /// <summary>Tipo de evento gravado no <see cref="EventLog"/>.</summary>
    public enum GasEventKind
    {
        EffectApplied,
        EffectRemoved,
        EffectStacksChanged,
        AbilityGranted,
        AbilityRemoved,
        CooldownChanged
    }

    /// <summary>
    /// Evento gravado. <see cref="Effect"/> vale nos eventos de efeito e <see cref="Ability"/> nos de habilidade e recarga;
    /// os demais campos, só no tipo a que se referem. <see cref="Time"/> é o <see cref="AbilitySystemComponent.TickedTime"/>.
    /// </summary>
    public readonly struct GasEventEntry
    {
        public readonly GasEventKind Kind;
        public readonly double Time;
        public readonly ActiveEffectInstance Effect;
        public readonly AbilitySpec Ability;
        public readonly EffectRemovalReason RemovalReason;
        public readonly int OldStacks;
        public readonly int NewStacks;
        public readonly CooldownChange CooldownChange;

        public GasEventEntry(GasEventKind kind, double time, ActiveEffectInstance effect, AbilitySpec ability,
            EffectRemovalReason removalReason = default, int oldStacks = 0, int newStacks = 0, CooldownChange cooldownChange = default)
        {
            Kind = kind;
            Time = time;
            Effect = effect;
            Ability = ability;
            RemovalReason = removalReason;
            OldStacks = oldStacks;
            NewStacks = newStacks;
            CooldownChange = cooldownChange;
        }
    }

    /// <summary>
    /// Últimos <see cref="Capacity"/> eventos de efeito, concessão e recarga de um ASC, num buffer circular: o mais antigo
    /// sai quando chega um novo. Grava entre <see cref="Attach"/> e <see cref="Detach"/>; gravar não aloca.
    /// </summary>
    public sealed class EventLog
    {
        /// <summary>Quantos eventos o log guarda.</summary>
        public const int Capacity = 50;

        private readonly GasEventEntry[] _buffer = new GasEventEntry[Capacity];
        private readonly Action<ActiveEffectInstance> _onEffectApplied;
        private readonly Action<ActiveEffectInstance, EffectRemovalReason> _onEffectRemoved;
        private readonly Action<ActiveEffectInstance, int, int> _onEffectStacksChanged;
        private readonly Action<AbilitySpec> _onAbilityGranted;
        private readonly Action<AbilitySpec> _onAbilityRemoved;
        private readonly Action<AbilitySpec, CooldownChange> _onCooldownChanged;
        private int _next;

        public EventLog()
        {
            _onEffectApplied = i => Add(new GasEventEntry(GasEventKind.EffectApplied, Time, i, null));
            _onEffectRemoved = (i, reason) => Add(new GasEventEntry(GasEventKind.EffectRemoved, Time, i, null, removalReason: reason));
            _onEffectStacksChanged = (i, before, after) =>
                Add(new GasEventEntry(GasEventKind.EffectStacksChanged, Time, i, null, oldStacks: before, newStacks: after));
            _onAbilityGranted = s => Add(new GasEventEntry(GasEventKind.AbilityGranted, Time, null, s));
            _onAbilityRemoved = s => Add(new GasEventEntry(GasEventKind.AbilityRemoved, Time, null, s));
            _onCooldownChanged = (s, change) =>
                Add(new GasEventEntry(GasEventKind.CooldownChanged, Time, null, s, cooldownChange: change));
        }

        /// <summary>ASC cujos eventos estão sendo gravados; nulo fora de <see cref="Attach"/>.</summary>
        public AbilitySystemComponent Target { get; private set; }

        /// <summary>Quantos eventos estão no log (no máximo <see cref="Capacity"/>).</summary>
        public int Count { get; private set; }

        /// <summary>Evento por ordem de chegada: 0 é o mais antigo, <c>Count - 1</c> o mais recente.</summary>
        public GasEventEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return _buffer[(_next - Count + index + Capacity) % Capacity];
            }
        }

        private double Time => Target != null ? Target.TickedTime : 0d;

        /// <summary>Passa a gravar os eventos de <paramref name="asc"/>, saindo do anterior. O log não é limpo.</summary>
        public void Attach(AbilitySystemComponent asc)
        {
            if (asc == Target) return;
            Detach();
            if (asc == null) return;

            Target = asc;
            asc.OnEffectApplied += _onEffectApplied;
            asc.OnEffectRemoved += _onEffectRemoved;
            asc.OnEffectStacksChanged += _onEffectStacksChanged;
            asc.OnAbilityGranted += _onAbilityGranted;
            asc.OnAbilityRemoved += _onAbilityRemoved;
            asc.OnCooldownChanged += _onCooldownChanged;
        }

        /// <summary>Para de gravar. Sem ASC, não faz nada.</summary>
        public void Detach()
        {
            AbilitySystemComponent asc = Target;
            Target = null;
            if (ReferenceEquals(asc, null)) return;

            asc.OnEffectApplied -= _onEffectApplied;
            asc.OnEffectRemoved -= _onEffectRemoved;
            asc.OnEffectStacksChanged -= _onEffectStacksChanged;
            asc.OnAbilityGranted -= _onAbilityGranted;
            asc.OnAbilityRemoved -= _onAbilityRemoved;
            asc.OnCooldownChanged -= _onCooldownChanged;
        }

        /// <summary>Esvazia o log, sem mudar a inscrição.</summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, Capacity);
            _next = 0;
            Count = 0;
        }

        private void Add(in GasEventEntry entry)
        {
            _buffer[_next] = entry;
            _next = (_next + 1) % Capacity;
            if (Count < Capacity) Count++;
        }
    }
}
