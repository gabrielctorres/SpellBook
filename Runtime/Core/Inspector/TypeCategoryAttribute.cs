using System;

namespace SpellBook.Core.Inspector
{
    /// <summary>
    /// Categoria e resumo de um tipo concreto no seletor de tipo (ADR 0026, item 4). Tipo sem o atributo fica na raiz.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TypeCategoryAttribute : Attribute
    {
        public TypeCategoryAttribute(string path)
        {
            Path = path;
        }

        /// <summary>Caminho da categoria, com <c>/</c> entre os níveis, por exemplo <c>"Fluxo/Espera"</c>.</summary>
        public string Path { get; }

        /// <summary>Resumo de uma linha, mostrado como dica.</summary>
        public string Summary { get; set; }
    }
}
