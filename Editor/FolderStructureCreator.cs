using UnityEditor;
using UnityEngine;
using System.IO;

namespace SpellBook.Editor
{
    public class FolderStructureCreator : UnityEditor.EditorWindow
    {
        [MenuItem("Tools/Create Default Project Folders")]
        public static void CreateProjectFolders()
        {

            string rootFolder = "Assets/_Project";


            string[] folders = {
            $"{rootFolder}/Resources",
            $"{rootFolder}/Resources/Prefab",
            $"{rootFolder}/Resources/Art",
            $"{rootFolder}/Resources/Sound",
            $"{rootFolder}/Scenes",
            $"{rootFolder}/Code",
            $"{rootFolder}/Code/Scripts",
            $"{rootFolder}/Code/Shader",
            $"{rootFolder}/Settings"
        };


            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    Debug.Log($"Pasta criada: {folder}");
                }
                else
                {
                    Debug.Log($"Pasta já existe: {folder}");
                }
            }


            AssetDatabase.Refresh();
            Debug.Log("Estrutura de pastas criada ou atualizada com sucesso!");
        }
    }
}

