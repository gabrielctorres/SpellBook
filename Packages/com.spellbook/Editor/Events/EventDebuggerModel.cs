using System;
using System.Collections.Generic;
using Void = SpellBook.Core.Void;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// Histórico do Event Debugger (R20). Enquanto grava, guarda cada disparo de canal com quadro, horário, payload e
    /// emissor num buffer circular de <see cref="Capacity"/> entradas (I-d). Só grava entre <see cref="Start"/> e
    /// <see cref="Stop"/>, que a janela chama ao abrir e fechar.
    /// </summary>
    internal sealed class EventDebuggerModel
    {
        /// <summary>Quantos disparos o histórico guarda. O mais antigo sai quando chega um novo.</summary>
        public const int Capacity = 256;

        /// <summary>Um disparo gravado. Payload e nomes viram texto na hora do disparo.</summary>
        public readonly struct Entry
        {
            public readonly int Frame;
            public readonly DateTime Time;
            public readonly EventChannel Channel;
            public readonly string ChannelName;
            public readonly string Payload;
            public readonly Object Sender;
            public readonly string SenderName;

            public Entry(int frame, DateTime time, EventChannel channel, object payload, Object sender)
            {
                Frame = frame;
                Time = time;
                Channel = channel;
                ChannelName = channel != null ? channel.name : "";
                Payload = payload == null || payload is Void ? "" : payload.ToString();
                Sender = sender;
                SenderName = sender != null ? sender.name : "";
            }
        }

        private readonly Entry[] _buffer = new Entry[Capacity];
        private readonly Func<int> _frame;
        private readonly Func<DateTime> _clock;
        private int _next;

        /// <summary>Modelo com o quadro da Unity e o relógio do sistema.</summary>
        public EventDebuggerModel() : this(() => UnityEngine.Time.frameCount, () => DateTime.Now)
        {
        }

        /// <summary>Modelo com fontes de quadro e horário próprias (testes).</summary>
        public EventDebuggerModel(Func<int> frame, Func<DateTime> clock)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>Quantos disparos estão no histórico.</summary>
        public int Count { get; private set; }

        /// <summary>Se o modelo está ouvindo os disparos dos canais.</summary>
        public bool IsRecording { get; private set; }

        /// <summary>Passa a gravar os disparos de todos os canais. Chamar de novo não duplica.</summary>
        public void Start()
        {
            if (IsRecording) return;
            EventChannel.Observed += Record;
            IsRecording = true;
        }

        /// <summary>Para de gravar. O histórico fica como está.</summary>
        public void Stop()
        {
            EventChannel.Observed -= Record;
            IsRecording = false;
        }

        /// <summary>Esvazia o histórico.</summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _next = 0;
            Count = 0;
        }

        /// <summary>
        /// Esvazia <paramref name="results"/> e põe nela os disparos cujo canal tem <paramref name="search"/> no nome,
        /// do mais recente ao mais antigo.
        /// </summary>
        public void GetEntries(string search, List<Entry> results)
        {
            results.Clear();
            for (int i = 1; i <= Count; i++)
            {
                Entry entry = _buffer[(_next - i + Capacity) % Capacity];
                if (Matches(entry.ChannelName, search))
                {
                    results.Add(entry);
                }
            }
        }

        /// <summary>Busca por nome: contém o termo, sem diferenciar maiúsculas. Termo vazio aceita tudo.</summary>
        public static bool Matches(string name, string search)
        {
            return string.IsNullOrEmpty(search)
                || (name != null && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void Record(EventChannel channel, object payload, Object sender)
        {
            _buffer[_next] = new Entry(_frame(), _clock(), channel, payload, sender);
            _next = (_next + 1) % Capacity;
            if (Count < Capacity) Count++;
        }
    }
}
