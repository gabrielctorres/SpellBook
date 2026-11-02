using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>Um assembly como a Unity o compila: nome, arquivos-fonte e DLLs que ele usa, com caminhos completos.</summary>
    public sealed class DocfxAssembly
    {
        public DocfxAssembly(string name, IReadOnlyList<string> sources, IReadOnlyList<string> references)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Sources = sources ?? throw new ArgumentNullException(nameof(sources));
            References = references ?? throw new ArgumentNullException(nameof(references));
        }

        public string Name { get; }

        public IReadOnlyList<string> Sources { get; }

        public IReadOnlyList<string> References { get; }
    }

    /// <summary>
    /// Configuração do <c>docfx metadata</c> para os assemblies do pacote (<see cref="TypeCatalog.IsPackageAssembly"/>).
    /// Os YAML saem em <c>api/reference/</c>, sem informação de git, para não levar caminho da máquina nem nome de branch.
    /// </summary>
    public static class DocfxConfig
    {
        /// <summary>Destino dos YAML, relativo à pasta da configuração (<c>api/obj/</c>).</summary>
        public const string Destination = "../reference";

        /// <summary>Assemblies que a Unity compila no Editor, com as fontes e as referências em caminho completo.</summary>
        public static IReadOnlyList<DocfxAssembly> Read()
        {
            return CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Select(a => new DocfxAssembly(a.name,
                    a.sourceFiles.Select(s => Path.GetFullPath(FileUtil.GetPhysicalPath(s))).ToList(),
                    a.allReferences.Select(Path.GetFullPath).ToList()))
                .ToList();
        }

        /// <summary>
        /// Pastas da biblioteca base do .NET da Unity para os níveis de compatibilidade dos assemblies do pacote, em caminho completo.
        /// </summary>
        /// <remarks>
        /// Cada assembly entra com dois níveis: o da API (<see cref="ScriptCompilerOptions.ApiCompatibilityLevel"/>) e o dos
        /// assemblies de Editor (<see cref="ScriptCompilerOptions.EditorAssembliesCompatibilityLevel"/>).
        /// </remarks>
        public static IReadOnlyList<string> ReadSystemAssemblyDirectories()
        {
            return CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Where(a => TypeCatalog.IsPackageAssembly(a.name))
                .SelectMany(a => new[]
                {
                    a.compilerOptions.ApiCompatibilityLevel,
                    ToApiLevel(a.compilerOptions.EditorAssembliesCompatibilityLevel),
                })
                .Distinct()
                .SelectMany(CompilationPipeline.GetSystemAssemblyDirectories)
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Texto do <c>docfx.metadata.json</c>. As fontes ficam relativas a <paramref name="configDir"/>; as referências são
        /// as DLLs dos assemblies do pacote, sem as saídas deles e sem as que estão em <paramref name="systemAssemblyDirectories"/>,
        /// porque o DocFX já traz a biblioteca base do .NET. Listas em ordem ordinal, barras <c>/</c> e quebras <c>\n</c>.
        /// </summary>
        public static string Render(IEnumerable<DocfxAssembly> assemblies, IReadOnlyCollection<string> systemAssemblyDirectories,
            string configDir)
        {
            List<DocfxAssembly> package = assemblies.Where(a => TypeCatalog.IsPackageAssembly(a.Name)).ToList();
            var outputs = new HashSet<string>(package.Select(a => a.Name), StringComparer.Ordinal);
            List<string> systemFolders = systemAssemblyDirectories.Select(d => Slashes(Path.GetFullPath(d)).TrimEnd('/') + "/").ToList();

            IEnumerable<string> sources = package.SelectMany(a => a.Sources)
                .Select(s => Slashes(Path.GetRelativePath(configDir, s)));
            IEnumerable<string> references = package.SelectMany(a => a.References)
                .Where(r => !outputs.Contains(Path.GetFileNameWithoutExtension(r)))
                .Select(r => Slashes(Path.GetFullPath(r)))
                .Where(r => !systemFolders.Any(d => r.StartsWith(d, StringComparison.OrdinalIgnoreCase)));

            var text = new StringBuilder();
            text.Append("{\n  \"metadata\": [\n    {\n");
            AppendMappings(text, "src", sources);
            text.Append(",\n");
            AppendMappings(text, "references", references);
            text.Append(",\n      \"disableGitFeatures\": true,\n");
            text.Append("      \"dest\": ").Append(Quote(Destination)).Append('\n');
            text.Append("    }\n  ]\n}\n");
            return text.ToString();
        }

        // O DocFX não aceita "../" no glob de "files", só em "src": um mapeamento por pasta.
        private static void AppendMappings(StringBuilder text, string key, IEnumerable<string> paths)
        {
            List<IGrouping<string, string>> folders = paths.Distinct(StringComparer.Ordinal)
                .GroupBy(p => p.Substring(0, Math.Max(p.LastIndexOf('/'), 0)), StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            text.Append("      ").Append(Quote(key)).Append(": [");
            for (int i = 0; i < folders.Count; i++)
            {
                IEnumerable<string> files = folders[i].Select(p => Quote(p.Substring(p.LastIndexOf('/') + 1)))
                    .OrderBy(f => f, StringComparer.Ordinal);
                text.Append(i == 0 ? "\n" : ",\n")
                    .Append("        { \"src\": ").Append(Quote(folders[i].Key))
                    .Append(", \"files\": [ ").Append(string.Join(", ", files)).Append(" ] }");
            }

            text.Append(folders.Count > 0 ? "\n      ]" : "]");
        }

        // Default equivale a NET_Unity_4_8. Um nível novo lança exceção em vez de sumir do filtro.
        private static ApiCompatibilityLevel ToApiLevel(EditorAssembliesCompatibilityLevel level)
        {
            switch (level)
            {
                case EditorAssembliesCompatibilityLevel.Default:
                case EditorAssembliesCompatibilityLevel.NET_Unity_4_8:
                    return ApiCompatibilityLevel.NET_Unity_4_8;
                case EditorAssembliesCompatibilityLevel.NET_Standard:
                    return ApiCompatibilityLevel.NET_Standard;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level,
                        "Nível de compatibilidade de Editor desconhecido: " + level + ".");
            }
        }

        private static string Slashes(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
