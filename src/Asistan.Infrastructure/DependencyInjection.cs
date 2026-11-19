using Asistan.Application.Kullanicilar;
using Asistan.Application.Sohbet;
using Asistan.Infrastructure.Llm;
using Asistan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asistan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Sql")));
        services.AddScoped<IKullaniciDeposu, KullaniciDeposu>();

        // Typed HttpClient: adres ve anahtar tek yerde, LLM çağrıları 120 sn'ye kadar bekler.
        services.AddHttpClient<ILlmIstemcisi, LiteLlmIstemcisi>(c =>
        {
            c.BaseAddress = new Uri(config["LiteLlm:BaseUrl"]!);
            c.DefaultRequestHeaders.Authorization = new("Bearer", config["LiteLlm:Key"]);
            c.Timeout = TimeSpan.FromSeconds(120);
        });
        return services;
    }
}
