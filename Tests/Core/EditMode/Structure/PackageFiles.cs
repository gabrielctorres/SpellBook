using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>
    /// Lê arquivos do pacote pelo caminho real, obtido de <c>PackageInfo.FindForAssembly</c> (risco 22).
    /// Pastas terminadas em <c>~</c> ou começadas por <c>.</c> ficam de fora, como na importação da Unity.
    /// </summary>
    internal static class PackageFiles
    {
        public static string Root
        {
            get
            {
                PackageInfo info = PackageInfo.FindForAssembly(typeof(PackageFiles).Assembly);
                if (info == null)
                {
                    throw new InvalidOperationException(
                        "O assembly de teste não pertence a um pacote; rode os testes no host com o pacote em file: e testables.");
                }

                return info.resolvedPath;
            }
        }

        /// <summary>Caminhos relativos à raiz (com <c>/</c>) dos arquivos com as extensões dadas.</summary>
        public static IReadOnlyList<string> Find(string relativeFolder, params string[] extensions)
        {
            string root = Root;
            string start = string.IsNullOrEmpty(relativeFolder) ? root : Path.Combine(root, relativeFolder);
            var result = new List<string>();
            if (!Directory.Exists(start))
            {
                return result;
            }

            var pending = new Stack<string>();
            pending.Push(start);
            while (pending.Count > 0)
            {
                string folder = pending.Pop();
                foreach (string file in Directory.GetFiles(folder))
                {
                    if (extensions.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(ToRelative(root, file));
                    }
                }

                foreach (string child in Directory.GetDirectories(folder))
                {
                    string name = Path.GetFileName(child);
                    if (!name.EndsWith("~", StringComparison.Ordinal) && !name.StartsWith(".", StringComparison.Ordinal))
                    {
                        pending.Push(child);
                    }
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public static string ReadText(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Root, relativePath));
        }

        public static IReadOnlyList<AsmdefFile> Asmdefs()
        {
            return Find(string.Empty, ".asmdef")
                .Select(path => new AsmdefFile(path, JsonUtility.FromJson<AsmdefData>(ReadText(path))))
                .ToList();
        }

        private static string ToRelative(string root, string file)
        {
            return file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
        }
    }

    internal enum AsmdefKind
    {
        Runtime,
        Editor,
        Test,
    }

    internal sealed class AsmdefFile
    {
        public AsmdefFile(string path, AsmdefData data)
        {
            Path = path;
            Data = data;
            Kind = path.StartsWith("Tests/", StringComparison.Ordinal) ? AsmdefKind.Test
                : data.includePlatforms.Length == 1 && data.includePlatforms[0] == "Editor" ? AsmdefKind.Editor
                : AsmdefKind.Runtime;
            ReferenceNames = data.references.Select(ResolveReference).ToList();
        }

        public string Path { get; }
        public AsmdefData Data { get; }
        public AsmdefKind Kind { get; }

        /// <summary>Referências com <c>GUID:</c> trocadas pelo nome do asmdef; as não resolvidas ficam como estão.</summary>
        public IReadOnlyList<string> ReferenceNames { get; }

        private static string ResolveReference(string reference)
        {
            const string prefix = "GUID:";
            if (!reference.StartsWith(prefix, StringComparison.Ordinal))
            {
                return reference;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(reference.Substring(prefix.Length));
            var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
            return asset == null ? reference : JsonUtility.FromJson<AsmdefData>(asset.text).name;
        }
    }

    /// <summary>Campos do <c>.asmdef</c> lidos pelos testes de estrutura (nomes iguais aos do JSON).</summary>
    [Serializable]
    internal sealed class AsmdefData
    {
        public string name = string.Empty;
        public string[] references = Array.Empty<string>();
        public string[] includePlatforms = Array.Empty<string>();
        public bool overrideReferences;
        public string[] precompiledReferences = Array.Empty<string>();
        public bool autoReferenced = true;
        public string[] defineConstraints = Array.Empty<string>();
        public VersionDefineData[] versionDefines = Array.Empty<VersionDefineData>();
    }

    [Serializable]
    internal sealed class VersionDefineData
    {
        public string name = string.Empty;
        public string expression = string.Empty;
        public string define = string.Empty;
    }
}
