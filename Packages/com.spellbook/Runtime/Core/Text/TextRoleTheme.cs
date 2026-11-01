using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>Papel semântico de um texto, que o <see cref="TextRoleTheme"/> traduz em cor (R19).</summary>
    public enum TextRole
    {
        /// <summary>Sem papel; usa a cor de <see cref="Neutral"/>.</summary>
        None,
        Beneficial,
        Harmful,
        Neutral,
        Value
    }

    /// <summary>
    /// Tema de cores por <see cref="TextRole"/>, compartilhado pelos textos do jogo em vez de cores por atributo (R19, R20).
    /// Não conhece o GAS (ADR 0012). Sem tema próprio, use <see cref="Default"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/Text Role Theme", fileName = "TextRoleTheme")]
    [HelpURL(DocUrls.Gas + "#textroletheme")]
    public class TextRoleTheme : ScriptableObject
    {
        [SerializeField] private Color beneficial = new Color(0.30f, 0.80f, 0.35f, 1f);
        [SerializeField] private Color harmful = new Color(0.90f, 0.30f, 0.30f, 1f);
        [SerializeField] private Color neutral = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color value = new Color(1.00f, 0.85f, 0.40f, 1f);

        private static TextRoleTheme _default;

        /// <summary>
        /// Tema padrão do pacote, com as cores iniciais de um tema novo. Criado sob demanda, fora de cena e sem asset em
        /// disco (<see cref="HideFlags.HideAndDontSave"/>).
        /// </summary>
        public static TextRoleTheme Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<TextRoleTheme>();
                    _default.name = "Default Text Role Theme";
                    _default.hideFlags = HideFlags.HideAndDontSave;
                }

                return _default;
            }
        }

        /// <summary>A cor do papel; <see cref="TextRole.None"/> usa a de <see cref="TextRole.Neutral"/>. Não aloca.</summary>
        public Color GetColor(TextRole role)
        {
            switch (role)
            {
                case TextRole.Beneficial: return beneficial;
                case TextRole.Harmful: return harmful;
                case TextRole.Value: return value;
                default: return neutral;
            }
        }
    }
}
