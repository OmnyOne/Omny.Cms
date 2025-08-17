using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Omny.Cms.Ui.Tests;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    public Uri? BaseUri { get; private set; }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        var testHost = builder.Build();

        builder.ConfigureWebHost(webHostBuilder => webHostBuilder.UseKestrel());

        var host = builder.Build();
        host.Start();

        var server = host.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        ClientOptions.BaseAddress = addresses!.Addresses.Select(x => new Uri(x)).Last(u => u.Scheme == "http");
        BaseUri = ClientOptions.BaseAddress;

        testHost.Start();
        return testHost;
    }
}
