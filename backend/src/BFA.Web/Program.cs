using BFA.Infrastructure;
using BFA.Infrastructure.Cobrancas;
using BFA.Web;
using BFA.Web.Bootstrap;
using BFA.Web.Franqueados;
using BFA.Web.Infrastructure;
using BFA.Web.Localidades;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBfaAuthorization();
builder.Services.AddScoped<BootstrapInicialCommand>();
builder.Services.AddScoped<SincronizarLocalidadesIbgeCommand>();
builder.Services.AddScoped<DiagnosticarVinculosFranqueadoCommand>();

var app = builder.Build();

if (BootstrapInicialCommand.Solicitado(args))
{
    await using var scope = app.Services.CreateAsyncScope();
    var command = scope.ServiceProvider.GetRequiredService<BootstrapInicialCommand>();
    Environment.ExitCode = await command.ExecutarAsync(Console.Out, Console.Error);
    return;
}

if (SincronizarLocalidadesIbgeCommand.Solicitado(args))
{
    await using var scope = app.Services.CreateAsyncScope();
    var command = scope.ServiceProvider.GetRequiredService<SincronizarLocalidadesIbgeCommand>();
    Environment.ExitCode = await command.ExecutarAsync(Console.Out, Console.Error);
    return;
}

if (DiagnosticarVinculosFranqueadoCommand.Solicitado(args))
{
    await using var scope = app.Services.CreateAsyncScope();
    var command = scope.ServiceProvider
        .GetRequiredService<DiagnosticarVinculosFranqueadoCommand>();
    Environment.ExitCode = await command.ExecutarAsync(Console.Out, Console.Error);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

var hangfireEnabled = builder.Configuration.GetValue<bool>("Hangfire:Enabled");

if (hangfireEnabled)
{
    var hangfireConfigured = app.Services.GetService<IRecurringJobManager>() is not null;

    if (hangfireConfigured)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            DashboardTitle = "BFA - Jobs"
        });

        RecurringJob.AddOrUpdate<GeracaoCobrancasJob>(
            "gerar-mensalidades",
            job => job.GerarMensalidadesAsync(CancellationToken.None),
            Cron.Daily,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")
            });

        RecurringJob.AddOrUpdate<GeracaoCobrancasJob>(
            "marcar-atrasadas",
            job => job.MarcarAtrasadasAsync(CancellationToken.None),
            Cron.Daily,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")
            });
    }
}

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
