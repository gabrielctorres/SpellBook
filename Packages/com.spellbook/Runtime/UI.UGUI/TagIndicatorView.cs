using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI do indicador de tag (R11): mostra o objeto com a tag ativa e o esconde apagado ou no estado vazio. Só
    /// desenha o modelo (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiTagIndicator)]
    public sealed class TagIndicatorView : MonoBehaviour, IView<TagIndicatorModel>
    {
        [SerializeField]
        [Tooltip("Objeto mostrado com a tag ativa, por exemplo o ícone de \"atordoado\". Use um filho, não o objeto desta view.")]
        private GameObject target;

        /// <inheritdoc />
        public void Render(in TagIndicatorModel model)
        {
            if (target == null) return;

            bool show = !model.IsEmpty && model.Active;
            if (target.activeSelf != show) target.SetActive(show);
        }
    }
}
