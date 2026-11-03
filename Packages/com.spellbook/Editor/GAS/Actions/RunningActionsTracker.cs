using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor.Actions
{
    /// <summary>
    /// Indicador de "rodando" das linhas de ação em Play (Spec do modelo evento → ações → alvos, Design 10a): a cada
    /// 200 ms, junta as ações em curso dos ASCs vivos e acende as <see cref="ActionRowView"/> sob a raiz. Fora do Play,
    /// não agenda nada e apaga as linhas.
    /// </summary>
    internal sealed class RunningActionsTracker
    {
        internal const long IntervalMs = 200;

        private readonly VisualElement _root;
        private readonly Func<IReadOnlyList<AbilitySystemComponent>> _source;
        private readonly Func<bool> _isPlaying;
        private readonly HashSet<GameplayAction> _running = new HashSet<GameplayAction>(ReferenceComparer.Instance);
        private readonly List<GameplayAction> _buffer = new List<GameplayAction>();
        private readonly Action<ActionRowView> _notify;
        private IVisualElementScheduledItem _item;

        /// <param name="source">ASCs lidos a cada passo; nulo, os da cena.</param>
        /// <param name="isPlaying">Se o Editor está em Play; nulo, <see cref="EditorApplication.isPlaying"/>.</param>
        internal RunningActionsTracker(VisualElement root, Func<IReadOnlyList<AbilitySystemComponent>> source = null,
            Func<bool> isPlaying = null)
        {
            _root = root;
            _source = source ?? (() => Object.FindObjectsByType<AbilitySystemComponent>(FindObjectsSortMode.None));
            _isPlaying = isPlaying ?? (() => EditorApplication.isPlaying);
            _notify = row => row.SetRunning(_running);
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                Sync();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                Stop();
            });
            Sync();
        }

        /// <summary>Ações em curso no último passo.</summary>
        internal IReadOnlyCollection<GameplayAction> Running => _running;

        /// <summary>Se o passo de 200 ms está agendado.</summary>
        internal bool IsScheduled { get; private set; }

        /// <summary>Lê as ações em curso de cada ASC e repassa o conjunto às linhas.</summary>
        internal void Update()
        {
            _running.Clear();
            IReadOnlyList<AbilitySystemComponent> ascs = _source();
            for (int i = 0; i < ascs.Count; i++)
            {
                if (ascs[i] == null) continue;
                ascs[i].CollectRunningActions(_buffer);
                _running.UnionWith(_buffer);
            }

            _root.Query<ActionRowView>().ForEach(_notify);
        }

        internal void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) Start();
            else Stop();
        }

        private void Sync()
        {
            if (_isPlaying()) Start();
            else Stop();
        }

        private void Start()
        {
            if (_item == null) _item = _root.schedule.Execute(Update).Every(IntervalMs);
            else _item.Resume();
            IsScheduled = true;
        }

        private void Stop()
        {
            _item?.Pause();
            IsScheduled = false;
            _running.Clear();
            _root.Query<ActionRowView>().ForEach(_notify);
        }

        // As ações podem sobrescrever Equals; a linha liga à instância do asset que roda
        private sealed class ReferenceComparer : IEqualityComparer<GameplayAction>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(GameplayAction x, GameplayAction y) => ReferenceEquals(x, y);

            public int GetHashCode(GameplayAction obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
