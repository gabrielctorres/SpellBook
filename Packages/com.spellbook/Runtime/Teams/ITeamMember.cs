namespace SpellBook.Teams
{
    /// <summary>
    /// Algo que pertence a um time. O <see cref="TeamMember"/> é a implementação pronta, mas qualquer tipo pode
    /// implementar a interface e entrar nas <see cref="TeamQueries"/>.
    /// </summary>
    public interface ITeamMember
    {
        /// <summary>Time atual. Nulo quando o membro não tem time.</summary>
        TeamDefinition Team { get; }

        /// <summary>Matriz usada quando este membro pergunta a relação com outro. Pode ser nula.</summary>
        TeamRelationMatrix Matrix { get; }
    }
}
