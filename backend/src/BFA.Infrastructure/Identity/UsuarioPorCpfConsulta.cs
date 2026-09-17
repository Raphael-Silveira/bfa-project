using BFA.Application.Acessos;
using BFA.Domain.Acessos;
using BFA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BFA.Infrastructure.Identity;

public sealed class UsuarioPorCpfConsulta(BfaDbContext dbContext)
    : IUsuarioPorCpfConsulta
{
    public Task<Guid?> ObterAlunoAsync(
        string cpfNormalizado,
        CancellationToken cancellationToken)
    {
        return (
            from aluno in dbContext.Alunos.AsNoTracking()
            join vinculo in dbContext.VinculosAcesso.AsNoTracking()
                on new { aluno.OrganizacaoId, UsuarioId = (Guid?)aluno.UsuarioId }
                equals new { vinculo.OrganizacaoId, UsuarioId = (Guid?)vinculo.UsuarioId }
            where aluno.Cpf == cpfNormalizado
                && aluno.Ativo
                && aluno.UsuarioId != null
                && vinculo.Perfil == PerfilAcesso.Aluno
                && vinculo.Ativo
            select aluno.UsuarioId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
