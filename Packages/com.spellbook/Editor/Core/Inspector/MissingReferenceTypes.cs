using System;
using System.Collections.Generic;
using UnityEditor;

namespace SpellBook.Core.Editor.Inspector
{
    /// <summary>Uma referência <c>[SerializeReference]</c> cujo tipo não existe mais (Spec, seção 4.10, D6).</summary>
    public readonly struct MissingTypeEntry
    {
        public MissingTypeEntry(long referenceId, string assemblyName, string namespaceName, string className)
        {
            ReferenceId = referenceId;
            AssemblyName = assemblyName;
            FullTypeName = string.IsNullOrEmpty(namespaceName) ? className : namespaceName + "." + className;
        }

        /// <summary>Id da referência no objeto; é o que <see cref="MissingReferenceTypes.Clear"/> recebe.</summary>
        public long ReferenceId { get; }

        /// <summary>Assembly gravado no asset.</summary>
        public string AssemblyName { get; }

        /// <summary><c>namespace.Classe</c>, ou só <c>Classe</c> quando o namespace é vazio.</summary>
        public string FullTypeName { get; }
    }

    /// <summary>
    /// Tipos ausentes de um objeto. O relato é por objeto: a Unity deixa o campo com <c>managedReferenceId = -2</c>,
    /// como um campo nulo, e nenhuma API pública liga o campo ao tipo ausente (Spec, D6). Nada no pacote limpa sozinho.
    /// </summary>
    public static class MissingReferenceTypes
    {
        private static readonly IReadOnlyList<MissingTypeEntry> None = Array.Empty<MissingTypeEntry>();

        /// <summary>Um item por tipo ausente, na ordem da Unity; lista vazia para alvo nulo ou sem tipo ausente.</summary>
        public static IReadOnlyList<MissingTypeEntry> Get(UnityEngine.Object target)
        {
            if (target == null || !SerializationUtility.HasManagedReferencesWithMissingTypes(target))
            {
                return None;
            }

            ManagedReferenceMissingType[] missing = SerializationUtility.GetManagedReferencesWithMissingTypes(target);
            var entries = new MissingTypeEntry[missing.Length];
            for (int i = 0; i < missing.Length; i++)
            {
                entries[i] = new MissingTypeEntry(missing[i].referenceId, missing[i].assemblyName,
                    missing[i].namespaceName, missing[i].className);
            }

            return entries;
        }

        /// <summary>Apaga os dados guardados da referência e marca o alvo como sujo. Só o botão do Inspector chama.</summary>
        public static bool Clear(UnityEngine.Object target, long referenceId)
        {
            bool cleared = SerializationUtility.ClearManagedReferenceWithMissingType(target, referenceId);
            EditorUtility.SetDirty(target);
            return cleared;
        }

        /// <summary>Texto do erro, igual no topo do Inspector e no validador do projeto (CA64, CA66).</summary>
        public static string FormatMessage(MissingTypeEntry entry)
        {
            return $"Tipo não encontrado: {entry.AssemblyName} {entry.FullTypeName}. "
                + "Restaure o tipo para recuperar os dados, ou limpe a referência.";
        }
    }
}
