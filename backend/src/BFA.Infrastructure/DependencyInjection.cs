using BFA.Application.Alunos;
using BFA.Application.Acessos;
using BFA.Application.Aulas;
using BFA.Application.Bootstrap;
using BFA.Application.Cobrancas;
using BFA.Application.Contratos;
using BFA.Application.Franqueadora;
using BFA.Application.Franqueadora.AcessosUnidade;
using BFA.Application.Franqueadora.Contratos;
using BFA.Application.Franqueadora.Franqueados;
using BFA.Application.Franqueadora.Unidades;
using BFA.Application.Franqueadora.Usuarios;
using BFA.Application.Identidade;
using BFA.Application.Localidades;
using BFA.Application.Matriculas;
using BFA.Application.Planos;
using BFA.Application.Professores.Turmas;
using BFA.Application.Unidades;
using BFA.Application.Unidades.Contratos;
using BFA.Application.Unidades.Professores;
using BFA.Application.Unidades.Turmas;
using BFA.Application.Usuarios;
using BFA.Infrastructure.Alunos;
using BFA.Infrastructure.Acessos;
using BFA.Infrastructure.Aulas;
using BFA.Infrastructure.Bootstrap;
using BFA.Infrastructure.Armazenamento;
using BFA.Infrastructure.Cobrancas;
using BFA.Infrastructure.Franqueadora;
using BFA.Infrastructure.Identity;
using BFA.Infrastructure.Localidades;
using BFA.Infrastructure.Matriculas;
using BFA.Infrastructure.Persistence;
using BFA.Infrastructure.Planos;
using BFA.Infrastructure.Professores;
using BFA.Infrastructure.Unidades;
using BFA.Infrastructure.Usuarios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BFA.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseConnectionName = "BfaDatabase";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<BfaDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(DatabaseConnectionName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql();
                return;
            }

            options.UseNpgsql(connectionString);
        });

        services.AddIdentityCore<UsuarioIdentity>(options =>
        {
            options.Stores.MaxLengthForKeys = 128;
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
        })
            .AddEntityFrameworkStores<BfaDbContext>()
            .AddDefaultTokenProviders()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "BFA.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/acesso-negado";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        services.AddScoped<IDatabaseConnectionProbe, DatabaseConnectionProbe>();
        services.Configure<ArmazenamentoDocumentosContratoOptions>(
            configuration.GetSection(
                ArmazenamentoDocumentosContratoOptions.SecaoConfiguracao));
        services.AddScoped<IArmazenamentoDocumentosContrato,
            ArmazenamentoLocalDocumentosContrato>();
        services.AddScoped<IAcessoUsuarioConsulta, AcessoUsuarioConsulta>();
        services.AddScoped<UnidadesUsuarioConsulta>();
        services.AddScoped<IUnidadesUsuarioConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<UnidadesUsuarioConsulta>());
        services.AddScoped<IUnidadeContextoConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<UnidadesUsuarioConsulta>());
        services.AddScoped<IEstadoOperacionalUnidadeConsulta,
            EstadoOperacionalUnidadeConsulta>();
        services.AddScoped<IGovernancaOperacionalUnidade,
            GovernancaOperacionalUnidadeServico>();
        services.AddScoped<IContratoUnidadeConsulta, ContratoUnidadeConsulta>();
        services.AddScoped<IProfessoresUnidadeRepositorio, ProfessoresUnidadeRepositorio>();
        services.AddScoped<IAcessoProfessorRepositorio, AcessoProfessorRepositorio>();
        services.AddScoped<IAcessoProfessorServico, AcessoProfessorServico>();
        services.AddScoped<ProfessoresUnidadeServico>();
        services.AddScoped<IProfessoresUnidadeConsulta>(provider =>
            provider.GetRequiredService<ProfessoresUnidadeServico>());
        services.AddScoped<IProfessoresUnidadeServico>(provider =>
            provider.GetRequiredService<ProfessoresUnidadeServico>());
        services.AddScoped<IMinhasTurmasProfessorRepositorio,
            MinhasTurmasProfessorRepositorio>();
        services.AddScoped<IMinhasTurmasProfessorConsulta,
            MinhasTurmasProfessorConsulta>();
        services.AddScoped<ITurmasUnidadeRepositorio, TurmasUnidadeRepositorio>();
        services.AddScoped<TurmasUnidadeServico>();
        services.AddScoped<ITurmasUnidadeConsulta>(provider =>
            provider.GetRequiredService<TurmasUnidadeServico>());
        services.AddScoped<ITurmasUnidadeServico>(provider =>
            provider.GetRequiredService<TurmasUnidadeServico>());
        services.AddScoped<IAjusteHorariosTurmaRepositorio,
            AjusteHorariosTurmaRepositorio>();
        services.AddScoped<IAjusteHorariosTurmaServico,
            AjusteHorariosTurmaServico>();
        services.AddScoped<ITrocaProfessorTurmaRepositorio,
            TrocaProfessorTurmaRepositorio>();
        services.AddScoped<ITrocaProfessorTurmaServico,
            TrocaProfessorTurmaServico>();
        services.AddScoped<IUsuarioApresentacaoConsulta, UsuarioApresentacaoConsulta>();
        services.AddScoped<IUsuarioPorEmailConsulta, UsuarioPorEmailConsulta>();
        services.AddScoped<IPrimeiroAcessoServico, PrimeiroAcessoServico>();
        services.AddScoped<IBootstrapInicial, BootstrapInicial>();
        services.AddScoped<IPainelFranqueadoraConsulta, PainelFranqueadoraConsulta>();
        services.AddScoped<IAcessosUnidadeRepositorio, AcessosUnidadeRepositorio>();
        services.AddScoped<AcessosUnidadeServico>();
        services.AddScoped<IAcessosUnidadeConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<AcessosUnidadeServico>());
        services.AddScoped<IAcessosUnidadeServico>(serviceProvider =>
            serviceProvider.GetRequiredService<AcessosUnidadeServico>());
        services.AddScoped<IUnidadesFranqueadoraRepositorio, UnidadesFranqueadoraRepositorio>();
        services.AddScoped<UnidadesFranqueadoraServico>();
        services.AddScoped<IUnidadesFranqueadoraConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<UnidadesFranqueadoraServico>());
        services.AddScoped<IUnidadesFranqueadoraServico>(serviceProvider =>
            serviceProvider.GetRequiredService<UnidadesFranqueadoraServico>());
        services.AddScoped<IUsuariosFranqueadoraRepositorio, UsuariosFranqueadoraRepositorio>();
        services.AddScoped<UsuariosFranqueadoraServico>();
        services.AddScoped<IUsuariosFranqueadoraConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<UsuariosFranqueadoraServico>());
        services.AddScoped<IUsuariosFranqueadoraServico>(serviceProvider =>
            serviceProvider.GetRequiredService<UsuariosFranqueadoraServico>());
        services.AddScoped<FranqueadosRepositorio>();
        services.AddScoped<IFranqueadosRepositorio>(serviceProvider =>
            serviceProvider.GetRequiredService<FranqueadosRepositorio>());
        services.AddScoped<IDiagnosticoVinculosFranqueadoConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<FranqueadosRepositorio>());
        services.AddScoped<FranqueadosServico>();
        services.AddScoped<IFranqueadosConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<FranqueadosServico>());
        services.AddScoped<IFranqueadosServico>(serviceProvider =>
            serviceProvider.GetRequiredService<FranqueadosServico>());
        services.AddScoped<IContratosFranquiaRepositorio, ContratosFranquiaRepositorio>();
        services.AddScoped<ContratosFranquiaServico>();
        services.AddScoped<IContratosFranquiaConsulta>(serviceProvider =>
            serviceProvider.GetRequiredService<ContratosFranquiaServico>());
        services.AddScoped<IContratosFranquiaServico>(serviceProvider =>
            serviceProvider.GetRequiredService<ContratosFranquiaServico>());
        services.AddScoped<IPlanosRepositorio, PlanosRepositorio>();
        services.AddScoped<IPlanosServico, PlanosServico>();
        services.AddScoped<IMatriculasRepositorio, MatriculasRepositorio>();
        services.AddScoped<IMatriculasServico, MatriculasServico>();
        services.AddScoped<IAlunosRepositorio, AlunosRepositorio>();
        services.AddScoped<IAlunosServico, AlunosServico>();
        services.AddScoped<IAulasRepositorio, AulasRepositorio>();
        services.AddScoped<IAulasServico, AulasServico>();
        services.AddScoped<ICobrancasRepositorio, CobrancasRepositorio>();
        services.AddScoped<ICobrancasServico, CobrancasServico>();
        services.AddHttpClient<IIbgeLocalidadesClient, IbgeLocalidadesClient>(httpClient =>
        {
            const string configurationKey = "Integracoes:Ibge:BaseUrl";
            var configuredBaseUrl = configuration[configurationKey];

            if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException(
                    $"Configuração obrigatória inválida: {configurationKey}.");
            }

            httpClient.BaseAddress = baseUri;
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ILocalidadesConsulta, LocalidadesConsulta>();
        services.AddScoped<ILocalidadesSincronizacaoRepositorio,
            LocalidadesSincronizacaoRepositorio>();
        services.AddScoped<ILocalidadesSincronizacaoServico,
            LocalidadesSincronizacaoServico>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
