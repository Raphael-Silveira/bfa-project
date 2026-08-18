namespace BFA.Application.Franqueadora;

public enum EstadoPainelFranqueadora
{
    Disponivel = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3
}

public sealed record PainelFranqueadoraResumo(
    Guid OrganizacaoId,
    string NomeOrganizacao,
    int TotalUnidades,
    int UnidadesAtivas,
    int AdministradoresRedeAtivos,
    int AdministradoresUnidadeAtivos);

public sealed record PainelFranqueadoraResultado(
    EstadoPainelFranqueadora Estado,
    PainelFranqueadoraResumo? Resumo)
{
    public static PainelFranqueadoraResultado Disponivel(
        PainelFranqueadoraResumo resumo)
    {
        ArgumentNullException.ThrowIfNull(resumo);
        return new PainelFranqueadoraResultado(EstadoPainelFranqueadora.Disponivel, resumo);
    }

    public static PainelFranqueadoraResultado SemAcesso()
    {
        return new PainelFranqueadoraResultado(EstadoPainelFranqueadora.SemAcesso, null);
    }

    public static PainelFranqueadoraResultado SelecaoOrganizacaoNecessaria()
    {
        return new PainelFranqueadoraResultado(
            EstadoPainelFranqueadora.SelecaoOrganizacaoNecessaria,
            null);
    }
}
