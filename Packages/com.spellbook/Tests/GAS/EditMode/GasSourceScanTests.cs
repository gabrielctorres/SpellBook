using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// O GAS conta tempo só pelo ASC ou por uma fonte de tempo: nenhum fonte de <c>Runtime/GAS</c> lê o
    /// <c>Time</c> da Unity nem espera com <c>WaitForSeconds</c> (CA10; Spec 4.4).
    /// </summary>
    public class GasSourceScanTests
    {
        // Time. sem qualificação (não casa SpellBook.Time.), UnityEngine.Time e WaitForSeconds(Realtime).
        private static readonly Regex Forbidden = new Regex(@"(?<![\w.])Time\.|\bUnityEngine\.Time\b|\bWaitForSeconds");

        // Corrotinas da Unity e as esperas do contexto antigo: as ações rodam pelo executor do ASC (ADR 0034).
        private static readonly Regex Coroutines =
            new Regex(@"\bIEnumerator\b|\b(Start|Stop)Coroutine\b|\bCoroutine\b|\bWaitSeconds\b|\bStartChildRoutine\b");

        // Arquivo que declara uma ação.
        private static readonly Regex ActionDeclaration = new Regex(@":\s*GameplayAction\b");

        // As bases antigas das ações, como nome inteiro (não casa ApplyEffectAction nem IEffectActionCleanup).
        private static readonly Regex OldClasses = new Regex(@"\b(AbilityAction|AbilityContext|EffectAction)\b");

        // Membro protected internal: o override muda de forma conforme o compilador (ADR 0034, emenda de 2026-11-02).
        private static readonly Regex ProtectedInternal = new Regex(@"\bprotected\s+internal\b");

        // Criação e destruição de objetos de gameplay fora do spawner (ADR 0015) e som solto (CA26 do B-014).
        private static readonly Regex GameplayObjects =
            new Regex(@"\bPoolManager\.Instance\b|\bInstantiate\s*\(|\bDestroy(Immediate)?\s*\(|\bPlayClipAtPoint\b");

        [Test]
        public void GasSources_DoNotReadUnityTimeOrWaitForSeconds()
        {
            Assert.That(Scan(Forbidden, Path.Combine("Runtime", "GAS")), Is.Empty);
        }

        /// <summary>
        /// Nada no GAS e no Bridges roda em corrotina, e nenhuma ação lê o relógio da Unity: o tempo é o do executor do
        /// ASC (CA10). No Bridges, o relógio é procurado só nos arquivos que declaram uma ação; o relay, por exemplo,
        /// lê o <c>frameCount</c> para agrupar disparos, e isso não é espera.
        /// </summary>
        [Test]
        public void NoCoroutineOrUnityClock_InActions()
        {
            string gas = Path.Combine("Runtime", "GAS");
            string bridges = Path.Combine("Runtime", "GAS.Bridges");
            var violations = new List<string>();
            violations.AddRange(Scan(Coroutines, gas));
            violations.AddRange(Scan(Coroutines, bridges));
            violations.AddRange(Scan(Forbidden, gas));
            violations.AddRange(Scan(Forbidden, bridges, onlyFilesMatching: ActionDeclaration));

            Assert.That(violations, Is.Empty);
        }

        /// <summary><c>AbilityAction</c>, <c>AbilityContext</c> e <c>EffectAction</c> saíram do pacote, arquivo e nome (CA1).</summary>
        [Test]
        public void OldClassesGone()
        {
            string root = PackageRoot();
            var violations = new List<string>();
            foreach (string file in new[] { "Abilities/AbilityAction.cs", "Abilities/AbilityContext.cs", "Effects/EffectAction.cs" })
            {
                if (File.Exists(Path.Combine(root, "Runtime", "GAS", file))) violations.Add($"Runtime/GAS/{file} existe");
            }
            violations.AddRange(Scan(OldClasses, "Runtime"));
            violations.AddRange(Scan(OldClasses, "Editor"));

            Assert.That(violations, Is.Empty);
        }

        /// <summary>
        /// Nenhum membro do GAS e do Bridges é <c>protected internal</c>. O override de um membro assim é
        /// <c>protected internal</c> no mesmo assembly e <c>protected</c> fora dele, e o docfx, que compila o pacote como
        /// um assembly só, recusa a forma que a Unity pede no Bridges. O ponto de extensão das ações é <c>protected</c>.
        /// </summary>
        [Test]
        public void NoProtectedInternal_InActions()
        {
            var violations = new List<string>();
            violations.AddRange(Scan(ProtectedInternal, Path.Combine("Runtime", "GAS")));
            violations.AddRange(Scan(ProtectedInternal, Path.Combine("Runtime", "GAS.Bridges")));

            Assert.That(violations, Is.Empty);
        }

        /// <summary>
        /// O thinker conta o tempo pelo ASC dele e cria e devolve objetos só pelo spawner: nenhum fonte de
        /// <c>Runtime/GAS/Thinkers</c> usa corrotina, o relógio da Unity, <c>Instantiate</c>, <c>Destroy</c>,
        /// a instância global do pool nem <c>PlayClipAtPoint</c> (CA26 do B-014; ADR 0014, 0015 e 0036).
        /// </summary>
        [Test]
        public void ThinkerSources_NoCoroutineTimeOrInstantiate()
        {
            string thinkers = Path.Combine("Runtime", "GAS", "Thinkers");
            var violations = new List<string>();
            violations.AddRange(Scan(Coroutines, thinkers));
            violations.AddRange(Scan(Forbidden, thinkers));
            violations.AddRange(Scan(GameplayObjects, thinkers));

            Assert.That(violations, Is.Empty);
        }

        /// <summary>
        /// Linhas dos <c>.cs</c> de <paramref name="folder"/>, relativa ao pacote, que casam com <paramref name="pattern"/>.
        /// Com <paramref name="onlyFilesMatching"/>, só os arquivos em que alguma linha casa com ele.
        /// </summary>
        private static List<string> Scan(Regex pattern, string folder, Regex onlyFilesMatching = null)
        {
            string root = PackageRoot();
            string path = Path.Combine(root, folder);
            Assert.That(Directory.Exists(path), Is.True, $"pasta não encontrada: {path}");
            var violations = new List<string>();

            foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                if (onlyFilesMatching != null && !Array.Exists(lines, onlyFilesMatching.IsMatch)) continue;
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

        /// <summary>Caminho real do pacote (risco 22); os testes rodam no host com o pacote em <c>file:</c>.</summary>
        private static string PackageRoot()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(GasSourceScanTests).Assembly);
            if (info == null)
            {
                throw new InvalidOperationException(
                    "O assembly de teste não pertence a um pacote; rode os testes no host com o pacote em file: e testables.");
            }

            return info.resolvedPath;
        }
    }
}
