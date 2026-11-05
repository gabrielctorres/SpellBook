using System;
using System.Collections.Generic;
using SpellBook.Tags;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Excluir do gerenciador (CA14): quem usa o item, pela busca do painel de referências, e, numa tag, as tags filhas.
    /// Apaga só o item. As referências que ficam vazias, inclusive o pai das filhas, o validador acusa. Sem UI.
    /// </summary>
    internal sealed class GasDeletePlan
    {
        public const string ChildTagLocation = "Tag filha";

        private readonly List<GasReferenceRow> _users = new List<GasReferenceRow>();

        private GasDeletePlan() { }

        public Object Target { get; private set; }

        /// <summary>Numa tag, primeiro as filhas pela hierarquia; depois quem usa o item, uma linha por objeto.</summary>
        public IReadOnlyList<GasReferenceRow> Users => _users;

        /// <summary>A mensagem que impede excluir; <c>null</c> se pode.</summary>
        public string Problem { get; private set; }

        public static GasDeletePlan Build(Object target)
        {
            var plan = new GasDeletePlan { Target = target };
            if (!AssetDatabase.GetAssetPath(target).StartsWith("Assets/", StringComparison.Ordinal))
            {
                plan.Problem = "Este asset fica fora de Assets e não pode ser excluído.";
            }

            if (target is GameplayTagDefinition parent)
            {
                var children = new List<GameplayTagDefinition>();
                foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GameplayTagDefinition)))
                {
                    var tag = AssetDatabase.LoadAssetAtPath<GameplayTagDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tag != null && tag != parent && tag.Matches(parent)) children.Add(tag);
                }

                children.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (GameplayTagDefinition child in children) plan._users.Add(new GasReferenceRow(child, child.name, ChildTagLocation));
            }

            foreach (GasReferenceRow row in GasReferenceList.UsedBy(target))
            {
                if (!plan._users.Exists(u => u.Target == row.Target)) plan._users.Add(row);
            }

            return plan;
        }

        /// <summary>Apaga o item.</summary>
        /// <exception cref="InvalidOperationException">O plano tem um problema, ou a Unity não apagou o asset.</exception>
        public void Apply()
        {
            if (Problem != null) throw new InvalidOperationException(Problem);
            string name = Target.name;
            if (!AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(Target))) throw new InvalidOperationException($"A Unity não apagou {name}.");
        }
    }
}
