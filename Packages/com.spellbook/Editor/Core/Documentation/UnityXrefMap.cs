using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>
    /// Mapa de referências cruzadas do DocFX (<c>xrefmap</c>) que liga os tipos da Unity usados pela API à documentação da
    /// Unity: o ScriptReference mais recente para os módulos do motor e a versão instalada para os pacotes <c>com.unity.*</c>.
    /// </summary>
    public static class UnityXrefMap
    {
        private const string ScriptReference = "https://docs.unity3d.com/ScriptReference/";
        private const string Packages = "https://docs.unity3d.com/Packages/";

        private static readonly Regex MajorMinor = new Regex(@"^(\d+)\.(\d+)(?:\.|-|$)");

        private static Dictionary<string, Type> s_types;

        /// <summary>
        /// Texto do mapa para as referências externas dos YAML em <paramref name="referenceDirectory"/>, a pasta que o
        /// <c>docfx metadata</c> escreve. As entradas saem em ordem ordinal de <c>uid</c>, com quebras <c>\n</c> e sem data.
        /// </summary>
        /// <exception cref="InvalidOperationException">Uma referência da Unity não foi achada nos assemblies carregados.</exception>
        public static string Render(string referenceDirectory)
        {
            var text = new StringBuilder("### YamlMime:XRefMap\nsorted: true\nreferences:\n");
            var unresolved = new List<string>();
            foreach (ExternalReference reference in ReadExternalReferences(referenceDirectory))
            {
                string href;
                try
                {
                    href = ResolveHref(reference);
                }
                catch (TypeNotFoundException)
                {
                    unresolved.Add(reference.Uid);
                    continue;
                }

                if (href != null)
                {
                    text.Append("- uid: ").Append(Scalar(reference.Uid)).Append('\n');
                    text.Append("  name: ").Append(Scalar(reference.Name)).Append('\n');
                    text.Append("  href: ").Append(href).Append('\n');
                }
            }

            if (unresolved.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Referências da Unity sem tipo nos assemblies carregados ({unresolved.Count}):\n" + string.Join("\n", unresolved));
            }

            return text.ToString();
        }

        /// <summary>
        /// Referências externas (<c>isExternal: true</c>) de tipo e de membro na seção <c>references</c> dos YAML, sem
        /// repetição e em ordem ordinal de <c>uid</c>. Os namespaces ficam fora, porque não têm página própria.
        /// </summary>
        internal static IReadOnlyList<ExternalReference> ReadExternalReferences(string referenceDirectory)
        {
            var references = new SortedDictionary<string, ExternalReference>(StringComparer.Ordinal);
            IEnumerable<string> files = Directory.GetFiles(referenceDirectory, "*.yml").OrderBy(f => f, StringComparer.Ordinal);
            foreach (string file in files)
            {
                bool inReferences = false;
                Entry entry = null;
                foreach (string line in File.ReadLines(file))
                {
                    if (!line.StartsWith(" ", StringComparison.Ordinal) && !line.StartsWith("-", StringComparison.Ordinal))
                    {
                        Add(references, entry);
                        entry = null;
                        inReferences = line == "references:";
                    }
                    else if (inReferences && line.StartsWith("- uid: ", StringComparison.Ordinal))
                    {
                        Add(references, entry);
                        entry = new Entry { Uid = Unquote(line.Substring(7)) };
                    }
                    else if (entry != null)
                    {
                        ReadKey(entry, line);
                    }
                }

                Add(references, entry);
            }

            return references.Values.ToList();
        }

        /// <summary>
        /// Endereço da página da Unity para <paramref name="reference"/>, ou <c>null</c> se ela não vem de um assembly da Unity.
        /// </summary>
        /// <exception cref="TypeNotFoundException">O nome é da Unity, mas o tipo não está nos assemblies carregados.</exception>
        internal static string ResolveHref(ExternalReference reference)
        {
            char kind = reference.CommentId[0];
            string name = reference.CommentId.Substring(2);
            int parameters = name.IndexOf('(');
            if (parameters >= 0)
            {
                name = name.Substring(0, parameters);
            }

            string typeName = name;
            string member = null;
            if (kind != 'T')
            {
                int genericArity = name.IndexOf("``", StringComparison.Ordinal);
                if (genericArity >= 0)
                {
                    name = name.Substring(0, genericArity);
                }

                int dot = name.LastIndexOf('.');
                typeName = name.Substring(0, dot);
                member = name.Substring(dot + 1);
            }

            if (!Types().TryGetValue(typeName, out Type type))
            {
                if (IsUnityName(typeName))
                {
                    throw new TypeNotFoundException(reference.Uid);
                }

                return null;
            }

            PackageInfo package = PackageInfo.FindForAssembly(type.Assembly);
            if (package != null && package.name.StartsWith("com.unity.", StringComparison.Ordinal)
                && !package.name.StartsWith("com.unity.modules.", StringComparison.Ordinal))
            {
                return PackageApiHref(package.name, package.version, typeName);
            }

            if (!IsUnityName(type.Assembly.GetName().Name))
            {
                return null;
            }

            string page = StripPrefix(typeName).Replace('`', '_');
            if (member == null || type.IsEnum || kind == 'P' && parameters >= 0)
            {
                // Valor de enum e indexador não seguem a regra de membro no ScriptReference: apontam para a página do tipo.
                return ScriptReference + page + ".html";
            }

            if (member == "#ctor")
            {
                return ScriptReference + page + "-ctor.html";
            }

            return ScriptReference + page + (kind == 'M' ? "." : "-") + member + ".html";
        }

        /// <summary>
        /// Página da API de um pacote <c>com.unity.*</c> na versão <c>maior.menor</c> de <paramref name="packageVersion"/>,
        /// a forma em que a Unity publica a documentação dos pacotes.
        /// </summary>
        /// <exception cref="InvalidOperationException">A versão não começa com dois números.</exception>
        internal static string PackageApiHref(string packageName, string packageVersion, string typeUid)
        {
            Match match = MajorMinor.Match(packageVersion ?? string.Empty);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"Versão '{packageVersion}' do pacote {packageName} sem maior.menor, para a referência {typeUid}.");
            }

            string version = match.Groups[1].Value + "." + match.Groups[2].Value;
            return Packages + packageName + "@" + version + "/api/" + typeUid.Replace('`', '-') + ".html";
        }

        private static string StripPrefix(string typeName)
        {
            foreach (string prefix in new[] { "UnityEngine.", "UnityEditor." })
            {
                if (typeName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return typeName.Substring(prefix.Length);
                }
            }

            return typeName;
        }

        private static bool IsUnityName(string name)
        {
            return name.StartsWith("UnityEngine", StringComparison.Ordinal)
                || name.StartsWith("UnityEditor", StringComparison.Ordinal)
                || name.StartsWith("Unity.", StringComparison.Ordinal);
        }

        /// <summary>Tipos dos assemblies carregados pelo nome no formato do <c>commentId</c> (tipo aninhado com ponto).</summary>
        private static Dictionary<string, Type> Types()
        {
            if (s_types != null)
            {
                return s_types;
            }

            var types = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic))
            {
                Type[] loaded;
                try
                {
                    loaded = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    loaded = exception.Types;
                }

                foreach (Type type in loaded)
                {
                    if (type?.FullName != null && !types.ContainsKey(type.FullName.Replace('+', '.')))
                    {
                        types.Add(type.FullName.Replace('+', '.'), type);
                    }
                }
            }

            s_types = types;
            return types;
        }

        private static void ReadKey(Entry entry, string line)
        {
            if (line.StartsWith("  commentId: ", StringComparison.Ordinal))
            {
                entry.CommentId = Unquote(line.Substring(13));
            }
            else if (line.StartsWith("  name: ", StringComparison.Ordinal))
            {
                entry.Name = Unquote(line.Substring(8));
            }
            else if (line == "  isExternal: true")
            {
                entry.IsExternal = true;
            }
        }

        private static void Add(SortedDictionary<string, ExternalReference> references, Entry entry)
        {
            if (entry == null || !entry.IsExternal || entry.CommentId == null || entry.CommentId.Length < 3
                || entry.CommentId[1] != ':' || "TMPFE".IndexOf(entry.CommentId[0]) < 0 || references.ContainsKey(entry.Uid))
            {
                return;
            }

            references.Add(entry.Uid, new ExternalReference(entry.Uid, entry.CommentId, entry.Name ?? entry.Uid));
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
            {
                return value.Substring(1, value.Length - 2).Replace("''", "'");
            }

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            }

            return value;
        }

        /// <summary>Escalar YAML: simples quando o YAML aceita, senão entre aspas simples.</summary>
        private static string Scalar(string value)
        {
            bool plain = value.Length > 0
                && "-?:,[]{}#&*!|>'\"%@` ".IndexOf(value[0]) < 0
                && !value.EndsWith(" ", StringComparison.Ordinal)
                && !value.EndsWith(":", StringComparison.Ordinal)
                && !value.Contains(": ")
                && !value.Contains(" #");
            return plain ? value : "'" + value.Replace("'", "''") + "'";
        }

        private sealed class Entry
        {
            public string Uid;
            public string CommentId;
            public string Name;
            public bool IsExternal;
        }
    }

    /// <summary>Referência externa lida da seção <c>references</c> de um YAML do <c>docfx metadata</c>.</summary>
    internal readonly struct ExternalReference
    {
        public ExternalReference(string uid, string commentId, string name)
        {
            Uid = uid;
            CommentId = commentId;
            Name = name;
        }

        /// <summary>Identificador da referência no DocFX.</summary>
        public string Uid { get; }

        /// <summary><c>T:</c>, <c>M:</c>, <c>P:</c>, <c>F:</c> ou <c>E:</c> seguido do nome completo.</summary>
        public string CommentId { get; }

        /// <summary>Nome curto que o DocFX mostra no link.</summary>
        public string Name { get; }
    }

    /// <summary>Referência com nome da Unity cujo tipo não está nos assemblies carregados.</summary>
    internal sealed class TypeNotFoundException : Exception
    {
        public TypeNotFoundException(string uid)
            : base($"Tipo da Unity não encontrado para a referência {uid}.")
        {
        }
    }
}
