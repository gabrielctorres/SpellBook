using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>Roda o <c>docfx metadata</c> com a configuração dada e lança exceção se o processo falhar.</summary>
    internal interface IDocfxMetadataRunner
    {
        void Run(string documentationRoot, string metadataConfigPath);
    }

    /// <summary>
    /// Gera a referência do pacote: a página do catálogo, a configuração do DocFX em <c>api/obj/</c>, os YAML da API em
    /// <c>api/reference/</c> e a impressão digital em <c>api/reference.sha256</c> (ADR 0040, item 2).
    /// </summary>
    public static class ReferenceGenerator
    {
        /// <summary>Caminho do item de menu que gera a referência.</summary>
        public const string MenuPath = "SpellBook/Documentação/Gerar referência";

        /// <summary>Gera a referência na pasta <c>Documentation~/</c> do projeto aberto e registra o resultado no log.</summary>
        [MenuItem(MenuPath)]
        public static void GenerateFromMenu()
        {
            try
            {
                Generate(DefaultDocumentationRoot());
                Debug.Log("Referência gerada. Commite o catálogo, a pasta api/reference/ e o api/reference.sha256.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>Entrada do <c>-executeMethod</c> em batchmode. Sai com 0 se gerou e com 1 se falhou.</summary>
        public static void GenerateFromCommandLine()
        {
            int code = 0;
            try
            {
                Generate(DefaultDocumentationRoot());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        /// <summary>Gera a referência em <paramref name="documentationRoot"/>, a pasta <c>Documentation~/</c> na raiz do repositório.</summary>
        public static void Generate(string documentationRoot)
        {
            Generate(documentationRoot, new DotnetDocfxRunner());
        }

        internal static void Generate(string documentationRoot, IDocfxMetadataRunner runner)
        {
            string root = Path.GetFullPath(documentationRoot);
            string repositoryRoot = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            IReadOnlyList<TypeCatalogSection> sections = TypeCatalog.Build();
            WarnMissingSummaries(sections);
            Write(Path.Combine(root, "manual", "referencia", "catalogo.md"), TypeCatalogPage.Render(sections));

            string configDir = Path.Combine(root, "api", "obj");
            string configPath = Path.Combine(configDir, "docfx.metadata.json");
            Write(configPath, DocfxConfig.Render(DocfxConfig.Read(), DocfxConfig.ReadSystemAssemblyDirectories(), configDir));

            // Nada da geração anterior fica: tipo removido não deixa YAML, e uma falha não deixa impressão digital velha.
            string fingerprintPath = Path.Combine(root, "api", "reference.sha256");
            File.Delete(fingerprintPath);
            string referenceDir = Path.Combine(root, "api", "reference");
            if (Directory.Exists(referenceDir))
            {
                Directory.Delete(referenceDir, true);
            }

            runner.Run(root, configPath);
            Write(fingerprintPath, ReferenceFingerprint.Compute(repositoryRoot) + "\n");
        }

        private static string DefaultDocumentationRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Documentation~"));
        }

        private static void WarnMissingSummaries(IReadOnlyList<TypeCatalogSection> sections)
        {
            List<string> missing = sections.SelectMany(s => s.Entries)
                .Where(e => string.IsNullOrEmpty(e.Summary))
                .Select(e => e.Type.FullName)
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (missing.Count > 0)
            {
                Debug.LogWarning($"Tipos do catálogo sem Summary no [TypeCategory] ({missing.Count}):\n" + string.Join("\n", missing));
            }
        }

        private static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }

    /// <summary>
    /// Roda <c>dotnet tool restore</c> e <c>dotnet docfx metadata --warningsAsErrors</c> em <c>Documentation~/</c>, sem shell
    /// e com argumentos fixos.
    /// </summary>
    internal sealed class DotnetDocfxRunner : IDocfxMetadataRunner
    {
        public void Run(string documentationRoot, string metadataConfigPath)
        {
            string config = Path.GetRelativePath(documentationRoot, metadataConfigPath).Replace('\\', '/');
            Execute(documentationRoot, "tool restore");
            Execute(documentationRoot, $"docfx metadata \"{config}\" --warningsAsErrors");
        }

        private static void Execute(string workingDirectory, string arguments)
        {
            var start = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var output = new StringBuilder();
            using (var process = new Process { StartInfo = start })
            {
                process.OutputDataReceived += (_, e) => Append(output, e.Data);
                process.ErrorDataReceived += (_, e) => Append(output, e.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"dotnet {arguments} saiu com o código {process.ExitCode}:\n{output}");
                }
            }
        }

        private static void Append(StringBuilder output, string line)
        {
            if (line == null)
            {
                return;
            }

            lock (output)
            {
                output.Append(line).Append('\n');
            }
        }
    }
}
