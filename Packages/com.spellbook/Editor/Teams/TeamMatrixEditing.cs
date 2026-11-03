using System.Collections.Generic;
using UnityEditor;

namespace SpellBook.Teams.Editor
{
    /// <summary>
    /// Edição das células de uma <see cref="TeamRelationMatrix"/> no Editor. Cada operação grava pelo
    /// <see cref="SerializedObject"/> e entra num grupo de Undo próprio.
    /// </summary>
    public static class TeamMatrixEditing
    {
        private const string Entries = "entries";
        private const string Teams = "teams";

        /// <summary>Lê a célula de <paramref name="from"/> para <paramref name="to"/>. Com par repetido, vale a última.</summary>
        public static bool TryGet(TeamRelationMatrix matrix, TeamDefinition from, TeamDefinition to, out TeamRelation relation)
        {
            return TryGet(new SerializedObject(matrix).FindProperty(Entries), from, to, out relation);
        }

        /// <summary>Grava a relação na célula do par. Cria a célula se ela não existe.</summary>
        public static void Set(TeamRelationMatrix matrix, TeamDefinition from, TeamDefinition to, TeamRelation relation)
        {
            Edit(matrix, "Definir relação", entries => Set(entries, from, to, relation));
        }

        /// <summary>
        /// Copia a célula de <paramref name="from"/> para <paramref name="to"/> na célula inversa. Sem a célula de
        /// origem, a inversa é removida e os dois sentidos usam o padrão.
        /// </summary>
        public static void Mirror(TeamRelationMatrix matrix, TeamDefinition from, TeamDefinition to)
        {
            Edit(matrix, "Espelhar relação", entries => Mirror(entries, from, to));
        }

        /// <summary>
        /// Deixa a matriz simétrica. Em cada par, vale a célula do time que vem antes na lista; sem ela, vale a do outro.
        /// </summary>
        public static void MirrorAll(TeamRelationMatrix matrix)
        {
            var serialized = new SerializedObject(matrix);
            SerializedProperty teamList = serialized.FindProperty(Teams);
            var teams = new List<TeamDefinition>();
            for (int i = 0; i < teamList.arraySize; i++)
            {
                if (teamList.GetArrayElementAtIndex(i).objectReferenceValue is TeamDefinition team && !teams.Contains(team))
                {
                    teams.Add(team);
                }
            }

            Edit(matrix, "Espelhar matriz", entries =>
            {
                for (int row = 0; row < teams.Count; row++)
                {
                    for (int column = row + 1; column < teams.Count; column++)
                    {
                        bool upper = TryGet(entries, teams[row], teams[column], out _);
                        if (upper || TryGet(entries, teams[column], teams[row], out _))
                        {
                            TeamDefinition source = upper ? teams[row] : teams[column];
                            Mirror(entries, source, upper ? teams[column] : teams[row]);
                        }
                    }
                }
            });
        }

        /// <summary>Remove a célula do par. O par volta a usar o padrão da matriz.</summary>
        public static void Clear(TeamRelationMatrix matrix, TeamDefinition from, TeamDefinition to)
        {
            Edit(matrix, "Limpar relação", entries => Clear(entries, from, to));
        }

        private static void Edit(TeamRelationMatrix matrix, string undoName, System.Action<SerializedProperty> change)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            var serialized = new SerializedObject(matrix);
            change(serialized.FindProperty(Entries));
            serialized.ApplyModifiedProperties();
        }

        private static bool TryGet(SerializedProperty entries, TeamDefinition from, TeamDefinition to, out TeamRelation relation)
        {
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (IsPair(entry, from, to))
                {
                    relation = (TeamRelation)entry.FindPropertyRelative("relation").enumValueIndex;
                    return true;
                }
            }

            relation = default;
            return false;
        }

        private static void Set(SerializedProperty entries, TeamDefinition from, TeamDefinition to, TeamRelation relation)
        {
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (IsPair(entry, from, to))
                {
                    entry.FindPropertyRelative("relation").enumValueIndex = (int)relation;
                    found = true;
                }
            }

            if (found)
            {
                return;
            }

            entries.arraySize++;
            SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.FindPropertyRelative("from").objectReferenceValue = from;
            added.FindPropertyRelative("to").objectReferenceValue = to;
            added.FindPropertyRelative("relation").enumValueIndex = (int)relation;
        }

        private static void Mirror(SerializedProperty entries, TeamDefinition from, TeamDefinition to)
        {
            if (TryGet(entries, from, to, out TeamRelation relation))
            {
                Set(entries, to, from, relation);
            }
            else
            {
                Clear(entries, to, from);
            }
        }

        private static void Clear(SerializedProperty entries, TeamDefinition from, TeamDefinition to)
        {
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                if (IsPair(entries.GetArrayElementAtIndex(i), from, to))
                {
                    entries.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static bool IsPair(SerializedProperty entry, TeamDefinition from, TeamDefinition to)
        {
            return entry.FindPropertyRelative("from").objectReferenceValue == from
                && entry.FindPropertyRelative("to").objectReferenceValue == to;
        }
    }
}
