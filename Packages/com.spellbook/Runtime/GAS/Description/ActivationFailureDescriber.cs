using System;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Text;

namespace SpellBook.GAS.Description
{
    /// <summary>
    /// Motivo de falha de ativação como <see cref="DescriptionLine"/> (R16): a chave do motivo, a tag quando houver e o
    /// papel <see cref="TextRole.Harmful"/>. O texto vem do provedor que a view escolhe; sem provedor, a linha mostra a chave.
    /// </summary>
    public static class ActivationFailureDescriber
    {
        /// <summary>
        /// Acrescenta a <paramref name="context"/> uma linha <see cref="DescriptionLineKind.Failure"/> com a chave da regra
        /// (<see cref="ActivationFailure.ReasonKey"/>) ou, sem ela, a do resultado (<see cref="GasTextKeys.ForResult"/>), e a
        /// <see cref="ActivationFailure.ReasonTag"/>. No sucesso, nada. Não aloca.
        /// </summary>
        public static void Describe(in ActivationFailure failure, DescriptionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (failure.IsSuccess) return;

            string key = string.IsNullOrEmpty(failure.ReasonKey) ? GasTextKeys.ForResult(failure.Result) : failure.ReasonKey;
            context.Lines.Add(new DescriptionLine(
                DescriptionLineKind.Failure, new LocalizedText(key, key), 0f, TextRole.Harmful, tag: failure.ReasonTag));
        }
    }
}
