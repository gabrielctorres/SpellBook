using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Structure;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.Repository.Documentation.Tests
{
    /// <summary>
    /// As ferramentas de documentação ficam no host do repositório, fora do pacote, e o <c>-executeMethod</c> do guia
    /// continua chegando à geração (CA1 de organizacao-dos-asmdefs).
    /// </summary>
    public class DocumentationToolsLocationTests
    {
        private static readonly Regex ExecuteMethod = new Regex(@"-executeMethod\s+(?<name>[\w.]+)");

        [Test]
        public void Generator_IsNotInThePackage()
        {
            Assembly assembly = typeof(ReferenceGenerator).Assembly;

            Assert.That(PackageInfo.FindForAssembly(assembly), Is.Null, "A geração não pode estar num pacote.");
            Assert.That(assembly.GetName().Name, Is.EqualTo("Repository.Documentation.Editor"));
        }

        [Test]
        public void ExecuteMethod_InGuide_ResolvesToGenerator()
        {
            string guide = File.ReadAllText(Path.Combine(PackageFiles.RepositoryRoot, "Documentation~", "guia-do-manual.md"));
            Match match = ExecuteMethod.Match(guide);
            Assert.That(match.Success, Is.True, "guia-do-manual.md não tem -executeMethod.");
            string name = match.Groups["name"].Value;
            int dot = name.LastIndexOf('.');

            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(name.Substring(0, dot)))
                .FirstOrDefault(t => t != null);
            MethodInfo method = type?.GetMethod(name.Substring(dot + 1), BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);

            Assert.That(type, Is.EqualTo(typeof(ReferenceGenerator)), $"{name} não resolve para a geração.");
            Assert.That(method, Is.Not.Null, $"{name} não é um método public static sem parâmetros.");
        }
    }
}
