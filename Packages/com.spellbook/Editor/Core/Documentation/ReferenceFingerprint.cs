using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>
    /// Impressão digital das entradas da geração da referência: as fontes dos assemblies do pacote, a versão da Unity e a
    /// versão travada do DocFX. Se ela muda, a referência commitada ficou desatualizada.
    /// </summary>
    public static class ReferenceFingerprint
    {
        /// <summary>Manifesto da ferramenta local do .NET, relativo à raiz do repositório.</summary>
        public const string ToolsManifest = "Documentation~/.config/dotnet-tools.json";

        /// <summary>SHA-256, em hexadecimal minúsculo, das fontes que a Unity compila hoje para o pacote.</summary>
        public static string Compute(string repositoryRoot)
        {
            IEnumerable<string> sources = DocfxConfig.Read()
                .Where(a => TypeCatalog.IsPackageAssembly(a.Name))
                .SelectMany(a => a.Sources);
            return Compute(repositoryRoot, sources, Application.unityVersion);
        }

        /// <summary>
        /// SHA-256 de cada fonte (caminho relativo a <paramref name="repositoryRoot"/> com <c>/</c> e conteúdo com quebras
        /// <c>\n</c>, em ordem ordinal do caminho), seguidas da versão da Unity e do manifesto das ferramentas.
        /// </summary>
        internal static string Compute(string repositoryRoot, IEnumerable<string> sourceFiles, string unityVersion)
        {
            string root = Path.GetFullPath(repositoryRoot);
            List<string> paths = sourceFiles
                .Select(s => Path.GetRelativePath(root, Path.GetFullPath(s)).Replace('\\', '/'))
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            var text = new StringBuilder();
            foreach (string path in paths)
            {
                text.Append(path).Append('\n').Append(ReadNormalized(Path.Combine(root, path))).Append('\n');
            }

            text.Append(unityVersion).Append('\n');
            text.Append(ReadNormalized(Path.Combine(root, ToolsManifest)));

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }

        private static string ReadNormalized(string path)
        {
            return File.ReadAllText(path).Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
