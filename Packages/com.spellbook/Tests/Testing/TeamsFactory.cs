#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SpellBook.Teams;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Testing
{
    /// <summary>
    /// Cria times, matrizes e membros para testes sem nenhum asset em disco. Tudo o que a factory cria é destruído no
    /// <see cref="Dispose"/>. Só compila no Editor (SerializedObject).
    /// </summary>
    public sealed class TeamsFactory : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();

        /// <summary>Time com o nome de exibição dado.</summary>
        public TeamDefinition CreateTeam(string displayName)
        {
            var team = ScriptableObject.CreateInstance<TeamDefinition>();
            team.name = displayName;
            _created.Add(team);

            var serialized = new SerializedObject(team);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return team;
        }

        /// <summary>Matriz com os times dados, sem células, e a relação padrão para os pares sem célula.</summary>
        public TeamRelationMatrix CreateMatrix(TeamRelation defaultRelation, params TeamDefinition[] teams)
        {
            var matrix = ScriptableObject.CreateInstance<TeamRelationMatrix>();
            matrix.name = "TeamRelationMatrix_Test";
            _created.Add(matrix);

            var serialized = new SerializedObject(matrix);
            SerializedProperty list = serialized.FindProperty("teams");
            list.arraySize = teams.Length;
            for (int i = 0; i < teams.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = teams[i];
            }

            serialized.FindProperty("defaultRelation").enumValueIndex = (int)defaultRelation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return matrix;
        }

        /// <summary>Acrescenta a célula <paramref name="from"/> → <paramref name="to"/>.</summary>
        public void SetCell(TeamRelationMatrix matrix, TeamDefinition from, TeamDefinition to, TeamRelation relation)
        {
            var serialized = new SerializedObject(matrix);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("from").objectReferenceValue = from;
            entry.FindPropertyRelative("to").objectReferenceValue = to;
            entry.FindPropertyRelative("relation").enumValueIndex = (int)relation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Muda a relação de "sem time" da matriz.</summary>
        public void SetNoTeamRelation(TeamRelationMatrix matrix, TeamRelation relation)
        {
            var serialized = new SerializedObject(matrix);
            serialized.FindProperty("noTeamRelation").enumValueIndex = (int)relation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Objeto novo com um <see cref="TeamMember"/> no time e na matriz dados (ambos podem ser nulos).</summary>
        public TeamMember CreateMember(TeamDefinition team, TeamRelationMatrix matrix, string name = "TeamMember_Test")
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            var member = gameObject.AddComponent<TeamMember>();

            return Configure(member, team, matrix);
        }

        /// <summary>
        /// Põe um <see cref="TeamMember"/> num objeto que já existe, como o de um ASC ou um pai dele. Quem criou o objeto
        /// cuida de destruí-lo.
        /// </summary>
        public TeamMember AddMember(GameObject gameObject, TeamDefinition team, TeamRelationMatrix matrix)
        {
            return Configure(gameObject.AddComponent<TeamMember>(), team, matrix);
        }

        private static TeamMember Configure(TeamMember member, TeamDefinition team, TeamRelationMatrix matrix)
        {
            var serialized = new SerializedObject(member);
            serialized.FindProperty("team").objectReferenceValue = team;
            serialized.FindProperty("matrix").objectReferenceValue = matrix;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return member;
        }

        public void Dispose()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_created[i]);
                else Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }
    }
}
#endif
