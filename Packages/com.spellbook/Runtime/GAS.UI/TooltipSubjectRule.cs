using SpellBook.Core.Validation;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Regra comum das fontes de tooltip (Design 6, R14): o assunto vazio só vale se o objeto validado tem um
    /// <see cref="UIItemContext"/>, onde o <c>TooltipTrigger</c> lê o assunto do item.
    /// </summary>
    internal static class TooltipSubjectRule
    {
        /// <summary>
        /// Erro em <paramref name="propertyPath"/> se <paramref name="subject"/> é nulo ou destruído e o objeto do
        /// <paramref name="result"/> não é um componente com <see cref="UIItemContext"/> no mesmo objeto.
        /// <paramref name="noun"/> nomeia o assunto ("habilidade") e <paramref name="article"/> vem antes dele ("a").
        /// </summary>
        public static void Validate(ValidationResult result, Object subject, string propertyPath, string article, string noun)
        {
            if (subject != null || (result.Context is Component component && component.TryGetComponent(out UIItemContext _)))
            {
                return;
            }

            result.AddError($"Fonte sem {noun} e sem UIItemContext no objeto. Escolha {article} {noun} ou use o tooltip " +
                "num item de lista.", propertyPath);
        }
    }
}
