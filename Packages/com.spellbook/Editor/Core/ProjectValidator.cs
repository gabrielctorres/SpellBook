using System;
using System.Collections.Generic;
using System.Reflection;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Editor
{
    /// <summary>
    /// Validador do projeto (ADR 0026, item 6; Spec, seção 4.9). Roda o mesmo <see cref="IValidatable"/> que o topo do
    /// Inspector (<see cref="SpellBookEditor.CollectMessages"/>) e acusa cada tipo <c>[SerializeReference]</c> ausente
    /// com o mesmo texto do Inspector. Não muda nenhum objeto: nunca limpa referência.
    /// </summary>
    public static class ProjectValidator
    {
        private const string ProjectFolder = "Assets";

        /// <summary>
        /// Valida cada alvo e, nos <see cref="GameObject"/>, cada componente. Uma exceção em <c>Validate</c> vira erro
        /// e a varredura continua. As mensagens são acrescentadas em <paramref name="messages"/>.
        /// </summary>
        public static void Validate(IEnumerable<Object> targets, List<ValidationMessage> messages)
        {
            var single = new Object[1];
            foreach (Object target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                if (target is GameObject gameObject)
                {
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        // Componente com script ausente vem nulo.
                        if (component != null)
                        {
                            ValidateOne(component, single, messages);
                        }
                    }
                }
                else
                {
                    ValidateOne(target, single, messages);
                }
            }
        }

        /// <summary>
        /// Regra de <c>[HelpURL]</c> (R35, último item; <see cref="HelpUrlRule"/>) sobre os tipos de
        /// <paramref name="assemblies"/>: cada violação vira um erro com o nome do tipo, cujo alvo é o
        /// <see cref="MonoScript"/> do tipo (nulo se o tipo não tem script próprio).
        /// </summary>
        public static void ValidateTypes(IEnumerable<Assembly> assemblies, List<ValidationMessage> messages)
        {
            foreach (HelpUrlViolation violation in HelpUrlRule.FindViolations(assemblies))
            {
                string text = $"Tipo {violation.Type.FullName} {violation.Reason}: aponte o [HelpURL] para a página do " +
                    "manual (DocUrls.Base).";
                messages.Add(new ValidationMessage(ValidationSeverity.Error, text, FindScript(violation.Type), null));
            }
        }

        /// <summary>
        /// Assets de <see cref="ScriptableObject"/> e prefabs da pasta <c>Assets</c>, mais os objetos das cenas abertas.
        /// Nos prefabs e nas cenas, entra cada <see cref="GameObject"/> da hierarquia.
        /// </summary>
        public static List<Object> CollectProjectTargets()
        {
            var targets = new List<Object>();
            var folders = new[] { ProjectFolder };

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", folders))
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    if (asset is ScriptableObject)
                    {
                        targets.Add(asset);
                    }
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                AddHierarchy(prefab, targets);
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    AddHierarchy(root, targets);
                }
            }

            return targets;
        }

        private static void ValidateOne(Object target, Object[] single, List<ValidationMessage> messages)
        {
            single[0] = target;
            messages.AddRange(SpellBookEditor.CollectMessages(single));

            // Só MonoBehaviour e ScriptableObject têm [SerializeReference]; nos componentes nativos (Transform, por
            // exemplo) a Unity loga erro ao consultar tipos ausentes.
            if (!(target is MonoBehaviour) && !(target is ScriptableObject))
            {
                return;
            }

            foreach (MissingTypeEntry entry in MissingReferenceTypes.Get(target))
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Error, MissingReferenceTypes.FormatMessage(entry),
                    target, null));
            }
        }

        // O script de um tipo tem o nome da classe; a busca por nome acha também scripts de outros tipos com nome
        // parecido, e o GetClass separa o certo.
        private static MonoScript FindScript(Type type)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript " + type.Name))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == type)
                {
                    return script;
                }
            }

            return null;
        }

        private static void AddHierarchy(GameObject root, List<Object> targets)
        {
            if (root == null)
            {
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                targets.Add(child.gameObject);
            }
        }
    }
}
