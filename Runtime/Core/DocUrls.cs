namespace SpellBook.Core
{
    /// <summary>
    /// Endereços do manual público. Única fonte da URL dentro do código (ADR 0007):
    /// todo <c>[HelpURL]</c> usa constantes derivadas de <see cref="Base"/>.
    /// </summary>
    public static class DocUrls
    {
        /// <summary>Raiz do site do manual; igual ao <c>documentationUrl</c> do <c>package.json</c>.</summary>
        public const string Base = "https://gabrielctorres.github.io/SpellBook/";

        public const string Install = Base + "inicio/instalacao/";
        public const string Concepts = Base + "inicio/conceitos/";
        public const string Migration = Base + "regras/migracao-2-para-3/";
        public const string Time = Base + "modulos/tempo/";
        public const string Events = Base + "modulos/eventos/";
        public const string Pooling = Base + "modulos/pooling/";
        public const string Tags = Base + "modulos/tags/";
        public const string Gas = Base + "modulos/gas/";
        public const string Validator = Base + "ferramentas/validador/";
        public const string EventDebugger = Base + "ferramentas/event-debugger/";
    }
}
