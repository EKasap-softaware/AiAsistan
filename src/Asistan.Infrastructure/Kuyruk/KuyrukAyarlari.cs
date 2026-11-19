using Asistan.Application.Kuyruk;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asistan.Infrastructure.Kuyruk;

// IIsYayinci'nin gerçek hali: MassTransit ile RabbitMQ'ya mesaj gönderir.
public class MassTransitIsYayinci(ISendEndpointProvider gonderici) : IIsYayinci
{
    public async Task AiIstegiGonderAsync(AiIstegi istek, CancellationToken ct = default)
    {
        // "queue:ai-istegi" = doğrudan bu KUYRUĞA gönder. Kuyruk yoksa MassTransit onu oluşturur.
        // (Publish kullansaydık mesaj bir exchange'e giderdi; Worker hiç çalışmamışsa kuyruk ve bağ olmadığı için
        //  mesaj "not routed" olup SESSİZCE kaybolurdu: panelde gördüğün şey.)
        var uc = await gonderici.GetSendEndpoint(new Uri($"queue:{KuyrukAdlari.AiIstegi}"));
        await uc.Send(istek, ct);
    }
}

public static class KuyrukKaydi
{
    // API ve Worker aynı RabbitMQ bağlantısını kurar. Fark: Worker "tüketici" (consumer) ekler, API eklemez.
    public static IServiceCollection AddKuyruk(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? tuketicileriKaydet = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? kuyruklariKur = null)
    {
        services.AddMassTransit(x =>
        {
            tuketicileriKaydet?.Invoke(x);

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(
                    config["RabbitMq:Host"],
                    ushort.Parse(config["RabbitMq:Port"] ?? "5672"),
                    config["RabbitMq:VirtualHost"] ?? "/",
                    h =>
                    {
                        h.Username(config["RabbitMq:Username"]!);
                        h.Password(config["RabbitMq:Password"]!);
                    });

                kuyruklariKur?.Invoke(ctx, cfg);
            });
        });

        services.AddScoped<IIsYayinci, MassTransitIsYayinci>();
        return services;
    }
}
