using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Omny.Cms.Ui.Tests;

public class LocalRepoFactory : CustomWebAppFactory
{
    private readonly string _path1;
    private readonly string _path2;
    private readonly string _bucketPrefix;
    private readonly string _cdnUrl;
    private readonly string _serviceUrl;

    public LocalRepoFactory(string path1, string path2, string serviceUrl, string cdnUrl, string bucketPrefix)
    {
        _path1 = path1;
        _path2 = path2;
        _bucketPrefix = bucketPrefix;
        _cdnUrl = cdnUrl;
        _serviceUrl = serviceUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // assign a random port
        int port = new Random().Next(5000, 6000);
        builder.UseUrls($"http://localhost:{port}");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            var dict = new Dictionary<string, string?>
            {
                ["Repositories:Available:0:Owner"] = "local",
                ["Repositories:Available:0:RepoName"] = "local",
                ["Repositories:Available:0:Name"] = "Local 1",
                ["Repositories:Available:0:Branch"] = "main",
                ["Repositories:Available:0:LocalPath"] = _path1,
                ["Repositories:Available:0:UserEmails:0"] = "test@example.com",

                ["Repositories:Available:1:Owner"] = "local",
                ["Repositories:Available:1:RepoName"] = "local2",
                ["Repositories:Available:1:Name"] = "Local 2",
                ["Repositories:Available:1:Branch"] = "main",
                ["Repositories:Available:1:LocalPath"] = _path2,
                ["Repositories:Available:1:ImageStorage:BucketServiceUrl"] = _serviceUrl,
                ["Repositories:Available:1:ImageStorage:BucketName"] = "test-bucket",
                ["Repositories:Available:1:ImageStorage:Region"] = "us-east-1",
                ["Repositories:Available:1:ImageStorage:AccessKey"] = "test",
                ["Repositories:Available:1:ImageStorage:SecretKey"] = "test",
                ["Repositories:Available:1:ImageStorage:CdnUrl"] = _cdnUrl,
                ["Repositories:Available:1:ImageStorage:BucketPrefix"] = _bucketPrefix,
                ["Repositories:Available:1:UserEmails:0"] = "test@example.com",

                ["Users:Admins:0:Email"] = "test@example.com",
                ["Auth0:Domain"] = "example.com",
                ["Auth0:ClientId"] = "client",
                ["ApiAuthorization:SecretKey"] = "testsecret"
            };
            config.AddInMemoryCollection(dict!);
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
