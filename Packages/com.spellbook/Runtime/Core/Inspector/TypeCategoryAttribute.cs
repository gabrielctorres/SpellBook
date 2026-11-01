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

        /// <summary>
        /// Modelo do resumo de uma linha da instância no Inspector: <c>{Campo}</c> mostra o campo serializado e
        /// <c>{Campo?texto}</c> mostra o texto quando o <c>bool</c> é verdadeiro. Exemplo: <c>"{Attribute} {Comparison} {Value}"</c>.
        /// </summary>
        public string Line { get; set; }
    }
}
