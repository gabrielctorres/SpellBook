using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEngine;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI do "+N" de uma lista (ADR 0039, item 6): mostra o alvo com itens ocultos e escreve a contagem sem criar
    /// <see cref="string"/>. Só desenha o modelo.
    /// </summary>
    [HelpURL(DocUrls.Ui + "#listoverflowview")]
    public sealed class ListOverflowView : MonoBehaviour, IView<ListOverflowModel>
    {
        [SerializeField]
        [Tooltip("Objeto mostrado com itens ocultos. Use um filho, não o objeto desta view.")]
        private GameObject target;

        [SerializeField]
        [Tooltip("Texto que recebe \"+N\". Opcional.")]
        private TMP_Text text;

        // Buffer do "+N", criado uma vez e escrito do zero a cada desenho
        private readonly TextBuffer _buffer = new TextBuffer(8);

        /// <inheritdoc />
        public void Render(in ListOverflowModel model)
        {
            bool show = model.Hidden > 0;
            if (target != null && target.activeSelf != show) target.SetActive(show);
            if (text == null) return;

            _buffer.Clear();
            if (!show)
            {
                RoleColors.Write(text, default);
                return;
            }

            int start = _buffer.Length;
            _buffer.Append("+");
            _buffer.Append(model.Hidden, "0", null);
            RoleColors.Write(text, _buffer.SpanFrom(start));
        }
    }
}
