using SpellBook.GAS.Abilities;

namespace SpellBook.GAS.Text
{
    /// <summary>
    /// Chaves de texto do GAS (ADR 0038, item 3), com texto nas tabelas <c>GasTexts.pt-BR</c> e <c>GasTexts.en</c> desta
    /// pasta. Liste as duas tabelas no provedor do projeto para traduzir os motivos de falha e os rótulos da descrição.
    /// </summary>
    public static class GasTextKeys
    {
        /// <summary>Habilidade nula (<see cref="AbilityActivationResult.InvalidAbility"/>).</summary>
        public const string ActivationInvalid = "gas.activation.invalid";

        /// <summary>Habilidade não concedida ao ASC (<see cref="AbilityActivationResult.NotGranted"/>).</summary>
        public const string ActivationNotGranted = "gas.activation.not_granted";

        /// <summary>A habilidade ainda está em execução (<see cref="AbilityActivationResult.AlreadyActive"/>).</summary>
        public const string ActivationAlreadyActive = "gas.activation.already_active";

        /// <summary>O ASC tem uma tag bloqueadora (<see cref="AbilityActivationResult.BlockedByTags"/>).</summary>
        public const string ActivationBlockedByTags = "gas.activation.blocked_by_tags";

        /// <summary>Falta uma tag exigida (<see cref="AbilityActivationResult.MissingRequiredTags"/>).</summary>
        public const string ActivationMissingRequiredTags = "gas.activation.missing_required_tags";

        /// <summary>Uma habilidade ativa bloqueia esta (<see cref="AbilityActivationResult.BlockedByActiveAbility"/>).</summary>
        public const string ActivationBlockedByActiveAbility = "gas.activation.blocked_by_active_ability";

        /// <summary>Em recarga (<see cref="AbilityActivationResult.OnCooldown"/>).</summary>
        public const string ActivationOnCooldown = "gas.activation.on_cooldown";

        /// <summary>Sem recurso para o custo (<see cref="AbilityActivationResult.CannotAffordCost"/>).</summary>
        public const string ActivationCannotAffordCost = "gas.activation.cannot_afford_cost";

        /// <summary>Regra de ativação sem chave própria falhou (<see cref="AbilityActivationResult.BlockedByCondition"/>).</summary>
        public const string ActivationBlockedByCondition = "gas.activation.blocked_by_condition";

        /// <summary>Sem carga disponível (<see cref="AbilityActivationResult.NoCharges"/>).</summary>
        public const string ActivationNoCharges = "gas.activation.no_charges";

        /// <summary>Chave padrão da <c>AttributeThresholdCondition</c>.</summary>
        public const string ActivationAttributeThreshold = "gas.activation.attribute_threshold";

        /// <summary>Chave padrão da <c>CastRangeCondition</c>.</summary>
        public const string ActivationCastRange = "gas.activation.cast_range";

        /// <summary>Chave padrão da <c>HasTargetPointCondition</c>.</summary>
        public const string ActivationNoTarget = "gas.activation.no_target";

        /// <summary>Chave padrão da <c>HasTargetsCondition</c>.</summary>
        public const string ActivationHasTargets = "gas.activation.has_targets";

        /// <summary>Chave padrão da <c>TagQueryCondition</c>.</summary>
        public const string ActivationTagQuery = "gas.activation.tag_query";

        /// <summary>Rótulo da linha de recarga.</summary>
        public const string DescriptionCooldown = "gas.description.cooldown";

        /// <summary>Rótulo da linha de duração.</summary>
        public const string DescriptionDuration = "gas.description.duration";

        /// <summary>Rótulo da duração infinita.</summary>
        public const string DescriptionPermanent = "gas.description.permanent";

        /// <summary>Rótulo da linha de cargas: <c>{0}</c> é o máximo e <c>{1}</c>, o intervalo de recarga.</summary>
        public const string DescriptionCharges = "gas.description.charges";

        /// <summary>Chave do motivo de <paramref name="result"/>; <c>null</c> no sucesso ou em valor fora do enum.</summary>
        public static string ForResult(AbilityActivationResult result)
        {
            switch (result)
            {
                case AbilityActivationResult.InvalidAbility: return ActivationInvalid;
                case AbilityActivationResult.NotGranted: return ActivationNotGranted;
                case AbilityActivationResult.AlreadyActive: return ActivationAlreadyActive;
                case AbilityActivationResult.BlockedByTags: return ActivationBlockedByTags;
                case AbilityActivationResult.MissingRequiredTags: return ActivationMissingRequiredTags;
                case AbilityActivationResult.BlockedByActiveAbility: return ActivationBlockedByActiveAbility;
                case AbilityActivationResult.OnCooldown: return ActivationOnCooldown;
                case AbilityActivationResult.CannotAffordCost: return ActivationCannotAffordCost;
                case AbilityActivationResult.BlockedByCondition: return ActivationBlockedByCondition;
                case AbilityActivationResult.NoCharges: return ActivationNoCharges;
                default: return null;
            }
        }
    }
}
