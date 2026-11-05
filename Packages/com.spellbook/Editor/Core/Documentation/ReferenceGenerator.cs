using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>
    /// Gera a página do catálogo de tipos em <c>manual/referencia/catalogo.md</c> (ADR 0040, itens 1 e 4; ADR 0044,
    /// item 7). Não há referência da API gerada (ADR 0044, item 6).
    /// </summary>
    public static class ReferenceGenerator
    {
        /// <summary>Caminho do item de menu que gera o catálogo.</summary>
        public const string MenuPath = "SpellBook/Documentação/Gerar referência";

        /// <summary>Gera o catálogo na pasta <c>Documentation~/</c> do projeto aberto e registra o resultado no log.</summary>
        [MenuItem(MenuPath)]
        public static void GenerateFromMenu()
        {
            try
            {
                Generate(DefaultDocumentationRoot());
                Debug.Log("Catálogo gerado. Commite o manual/referencia/catalogo.md.");
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

        /// <summary>Gera o catálogo em <paramref name="documentationRoot"/>, a pasta <c>Documentation~/</c> na raiz do repositório.</summary>
        public static void Generate(string documentationRoot)
        {
            string root = Path.GetFullPath(documentationRoot);
            IReadOnlyList<TypeCatalogSection> sections = TypeCatalog.Build();
            WarnMissingSummaries(sections);
            Write(Path.Combine(root, "manual", "referencia", "catalogo.md"), TypeCatalogPage.Render(sections));
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
}
