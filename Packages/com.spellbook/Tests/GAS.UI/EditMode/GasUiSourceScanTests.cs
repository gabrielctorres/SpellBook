using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Os módulos de UI não usam singleton, busca na cena, <c>Instantiate</c> nem <c>Destroy</c>, o relógio da Unity nem
    /// corrotina (CA15): o dono vem do resolvedor, os números nascem pelo spawner e o tempo vem do driver.
    /// </summary>
    public class GasUiSourceScanTests
    {
        private static readonly string[] Folders =
        {
            Path.Combine("Runtime", "UI"),
            Path.Combine("Runtime", "UI.UGUI"),
            Path.Combine("Runtime", "GAS.UI"),
        };

        private static readonly string[] Assemblies = { "SpellBook.UI", "SpellBook.UI.UGUI", "SpellBook.GAS.UI" };

        // Busca na cena pelos nomes da Unity, com Resources.FindObjectsOfTypeAll (não casa um método próprio como
        // FloatingTextRules.Find).
        private static readonly Regex SceneSearch = new Regex(
            @"\bGameObject\.Find\w*\s*\(|\bFind(Any|First)?Objects?(OfType|ByType)(All)?\s*[(<]|\bFindWithTag\s*\(|\bFindGameObjectsWithTag\s*\(");

        // Criar ou destruir objeto direto, sem spawner, também na forma genérica Instantiate<T>(. Não casa o nome num
        // comentário como <c>Instantiate</c>: o argumento genérico não aceita a barra do fecha-tag.
        private static readonly Regex CreateOrDestroy =
            new Regex(@"\b(Instantiate|Destroy|DestroyImmediate)\s*(<[\w\s.,<>]*>\s*)?\(");

        // Time. sem qualificação (não casa SpellBook.Time.) e UnityEngine.Time.
        private static readonly Regex UnityTime = new Regex(@"(?<![\w.])Time\.|\bUnityEngine\.Time\b");

        private static readonly Regex Coroutines = new Regex(@"\bIEnumerator\b|\b(Start|Stop)Coroutine\b|\bCoroutine\b");

        // Campos estáticos mutáveis aceitos, com o motivo.
        private static readonly string[] MutableStaticAllowed =
        {
            // Gancho de Editor (internal, só com UNITY_EDITOR) que o [InitializeOnLoad] do SpellBook.UI.Editor liga à cena
            // de prévia do Prefab Mode; não guarda estado de jogo. Mesmo padrão do GameplayThinker (UI13).
            "SpellBook.UI.ActorResolver.IsInPreviewScene",
        };

        [Test]
        public void UiSources_NoFindInstantiateDestroyUnityTimeOrCoroutine()
        {
            var violations = new List<string>();
            foreach (string folder in Folders)
            {
                violations.AddRange(Scan(SceneSearch, folder));
                violations.AddRange(Scan(CreateOrDestroy, folder));
                violations.AddRange(Scan(UnityTime, folder));
                violations.AddRange(Scan(Coroutines, folder));
            }

            Assert.That(violations, Is.Empty);
        }

        /// <summary>
        /// Nenhum tipo dos módulos de UI guarda estado em campo estático que não é <c>const</c> nem <c>readonly</c>, o que
        /// pega o singleton e o <c>static event</c>. O cache de lambda que o compilador gera fica de fora.
        /// </summary>
        [Test]
        public void UiAssemblies_NoMutableStatic_ExceptEditorHook()
        {
            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var violations = new List<string>();
            foreach (string name in Assemblies)
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == name);
                Assert.That(assembly, Is.Not.Null, $"o assembly {name} não foi compilado");
                foreach (Type type in assembly.GetTypes().Where(t => !IsCompilerGenerated(t)))
                {
                    violations.AddRange(type.GetFields(statics)
                        .Where(field => !field.IsLiteral && !field.IsInitOnly)
                        .Select(field => $"{type.FullName}.{field.Name}")
                        .Where(field => !MutableStaticAllowed.Contains(field)));
                }
            }

            Assert.That(violations, Is.Empty);
        }

        private static bool IsCompilerGenerated(Type type)
        {
            for (Type current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
            }

            return false;
        }

        /// <summary>Linhas dos <c>.cs</c> de <paramref name="folder"/>, relativa ao pacote, que casam com <paramref name="pattern"/>.</summary>
        private static List<string> Scan(Regex pattern, string folder)
        {
            string root = PackageRoot();
            string path = Path.Combine(root, folder);
            Assert.That(Directory.Exists(path), Is.True, $"pasta não encontrada: {path}");
            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (pattern.IsMatch(lines[i]))
                    {
                        string relative = file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
                        violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }

            return violations;
        }

        /// <summary>Caminho real do pacote; os testes rodam no host com o pacote em <c>file:</c>.</summary>
        private static string PackageRoot()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(GasUiSourceScanTests).Assembly);
            if (info == null)
            {
                throw new InvalidOperationException(
                    "O assembly de teste não pertence a um pacote; rode os testes no host com o pacote em file: e testables.");
            }

            return info.resolvedPath;
        }
    }
}
