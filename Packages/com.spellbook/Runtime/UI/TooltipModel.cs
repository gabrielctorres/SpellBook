using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>O que uma linha do tooltip mostra; a view pode agrupar ou estilizar por tipo.</summary>
    public enum TooltipLineKind
    {
        Description,
        Modifier,
        Cost,
        Cooldown,
        Duration,
        Charges,
        Value,
        Breakdown
    }

    /// <summary>
    /// Uma linha do tooltip (Spec UI-3, Design 1): rótulo já resolvido pelo provedor, valor escrito no buffer da ligação,
    /// papel e assunto. Sem cor nem rich text: a cor vem do papel, escolhida pela view.
    /// </summary>
    public readonly struct TooltipLine
    {
        /// <summary>Cria a linha.</summary>
        public TooltipLine(string label, TextSpan value, TextRole role, TooltipLineKind kind, ScriptableObject subject = null)
        {
            Label = label;
            Value = value;
            Role = role;
            Kind = kind;
            Subject = subject;
        }

        /// <summary>Rótulo, já resolvido pelo provedor; pode ser nulo.</summary>
        public string Label { get; }

        /// <summary>Valor, escrito no buffer da ligação; vazio sem valor.</summary>
        public TextSpan Value { get; }

        /// <summary>Papel do texto, que o tema traduz em cor.</summary>
        public TextRole Role { get; }

        /// <summary>O que a linha mostra.</summary>
        public TooltipLineKind Kind { get; }

        /// <summary>O assunto da linha, como o atributo de um modificador, ou <c>null</c>.</summary>
        public ScriptableObject Subject { get; }
    }

    /// <summary>
    /// Modelo do tooltip (Spec UI-3, Design 1 e P1). A lista de linhas e o buffer dos valores são da ligação e são
    /// reaproveitados: a view lê tudo dentro do <c>Render</c> e não guarda o modelo. O <c>default</c> é o estado vazio
    /// (tooltip fechado).
    /// </summary>
    public readonly struct TooltipModel
    {
        private readonly IReadOnlyList<TooltipLine> _lines;
        private readonly bool _hasValue;

        /// <summary>Cria o modelo. Sem <paramref name="lines"/>, a lista é vazia.</summary>
        public TooltipModel(string title, Sprite icon, IReadOnlyList<TooltipLine> lines, string subtitle = null, string footer = null)
        {
            Title = title;
            Icon = icon;
            _lines = lines;
            Subtitle = subtitle;
            Footer = footer;
            HasStacks = false;
            Stacks = 0;
            HasRemaining = false;
            Remaining = 0f;
            RemainingText = default;
            _hasValue = true;
        }

        private TooltipModel(in TooltipModel source, bool hasStacks, int stacks, bool hasRemaining, float remaining, TextSpan remainingText)
        {
            this = source;
            HasStacks = hasStacks;
            Stacks = stacks;
            HasRemaining = hasRemaining;
            Remaining = remaining;
            RemainingText = remainingText;
        }

        /// <summary>Título, já resolvido pelo provedor.</summary>
        public string Title { get; }

        /// <summary>Subtítulo (a categoria, por exemplo), ou <c>null</c>.</summary>
        public string Subtitle { get; }

        /// <summary>Rodapé, ou <c>null</c>.</summary>
        public string Footer { get; }

        /// <summary>Ícone, ou <c>null</c>.</summary>
        public Sprite Icon { get; }

        /// <summary>As linhas, na ordem; nunca nula.</summary>
        public IReadOnlyList<TooltipLine> Lines => _lines ?? Array.Empty<TooltipLine>();

        /// <summary>Verdadeiro quando o modelo mostra pilhas.</summary>
        public bool HasStacks { get; }

        /// <summary>Pilhas; 0 sem pilhas.</summary>
        public int Stacks { get; }

        /// <summary>Verdadeiro quando o modelo mostra o tempo restante.</summary>
        public bool HasRemaining { get; }

        /// <summary>Segundos restantes; 0 sem tempo.</summary>
        public float Remaining { get; }

        /// <summary>Tempo restante escrito no buffer da ligação.</summary>
        public TextSpan RemainingText { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>): tooltip fechado.</summary>
        public bool IsEmpty => !_hasValue;

        /// <summary>Cópia com <paramref name="stacks"/> pilhas (negativo vale 0).</summary>
        public TooltipModel WithStacks(int stacks) =>
            new TooltipModel(this, true, Math.Max(0, stacks), HasRemaining, Remaining, RemainingText);

        /// <summary>Cópia com o tempo restante em segundos (negativo vale 0) e o texto dele.</summary>
        public TooltipModel WithRemaining(float seconds, TextSpan text) =>
            new TooltipModel(this, HasStacks, Stacks, true, Mathf.Max(0f, seconds), text);
    }
}
