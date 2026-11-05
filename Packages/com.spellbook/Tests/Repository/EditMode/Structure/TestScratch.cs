using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>
    /// Pasta de trabalho de uma classe de teste em <c>&lt;host&gt;/Temp/SpellBookTests/&lt;nome&gt;</c>.
    /// Cada projeto host tem a própria pasta, então duas Unity abertas em worktrees diferentes
    /// não apagam os arquivos uma da outra (Spec referencia-catalogo-e-api, emenda de 2026-11-02; XR3).
    /// </summary>
    public static class TestScratch
    {
        private const string TempFolder = "Temp";
        private const string ScratchFolder = "SpellBookTests";

        private static readonly Regex ValidName = new Regex("^[A-Za-z0-9-]+$");

        /// <summary>Pasta <paramref name="name"/> no projeto host aberto.</summary>
        public static string For(string name)
        {
            return For(Path.Combine(Application.dataPath, ".."), name);
        }

        /// <summary>Pasta <paramref name="name"/> no projeto host em <paramref name="hostRoot"/>.</summary>
        public static string For(string hostRoot, string name)
        {
            if (name == null || !ValidName.IsMatch(name))
            {
                throw new ArgumentException($"Nome de pasta de teste inválido: \"{name}\". Use só letras, dígitos e '-'.", nameof(name));
            }

            return Path.GetFullPath(Path.Combine(hostRoot, TempFolder, ScratchFolder, name));
        }

        /// <summary>Apaga a pasta, se existir, cria de novo e devolve o caminho.</summary>
        public static string Recreate(string path)
        {
            Delete(path);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>Apaga a pasta, se existir.</summary>
        public static void Delete(string path)
        {
            EnsureInsideScratch(path);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void EnsureInsideScratch(string path)
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (string parent = Path.GetDirectoryName(full); parent != null; parent = Path.GetDirectoryName(parent))
            {
                if (Path.GetFileName(parent) == ScratchFolder && Path.GetFileName(Path.GetDirectoryName(parent)) == TempFolder)
                {
                    return;
                }
            }

            throw new InvalidOperationException($"O caminho \"{path}\" não está dentro de {TempFolder}/{ScratchFolder}/; nada foi apagado.");
        }
    }
}
