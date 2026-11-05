namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// Nomes de categoria do NUnit usados pelos filtros <c>-testCategory</c> da suíte (Spec <c>processo-doc-no-fim.md</c>).
    /// </summary>
    public static class TestCategories
    {
        /// <summary>
        /// Guardas que comparam o código com o manual ou com a referência commitada. Rodam só no passe de documentação.
        /// </summary>
        public const string Documentation = "Documentacao";
    }
}
