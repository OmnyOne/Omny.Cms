using Microsoft.Playwright;
using System.Net.Http.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Omny.Cms.Ui.Tests;

public class PlaywrightFixture : IDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private TempDir? _tempDir1;
    private TempDir? _tempDir2;
    private string? _bucketPrefix;
    public LocalRepoFactory? Factory { get; private set; }
    public HttpClient? Client { get; private set; }
    public IBrowser Browser => _browser!;
    public bool AspireFailed { get; private set; }

    private record TokenResponse(string Token);

    public async Task StartAsync()
    {
        string serviceUrl;
        string dbConnectionString;
        try
        {
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Omny_Aspire_AppHost>();
            await using var app = await builder.BuildAsync();
            await app.StartAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var resource = await app.ResourceNotifications.WaitForResourceHealthyAsync(
                "localstack",
                cts.Token);
            if (!resource.Resource.TryGetUrls(out var urls))
            {
                throw new Exception("localstack URL not found");
            }

            serviceUrl = urls.First().Url;

            var dbResource = await app.ResourceNotifications.WaitForResourceHealthyAsync("filesdb", cts.Token);
            if (!dbResource.Resource.TryGetConnectionString(out var conn))
            {
                throw new Exception("filesdb connection string not found");
            }
            dbConnectionString = conn;

            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            };

            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials("test", "test");
            var s3Client = new AmazonS3Client(awsCredentials, config);
            try
            {
                await s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = "test-bucket"
                }, cts.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var listBucketsResponse = await s3Client.ListBucketsAsync(cts.Token);
            if (listBucketsResponse.Buckets.All(b => b.BucketName != "test-bucket"))
            {
                throw new Exception("Test bucket 'test-bucket' not found in LocalStack S3.");
            }

            var getBucketPolicyResponse = await s3Client.GetBucketPolicyAsync("test-bucket", cts.Token);
            if (getBucketPolicyResponse.Policy is null)
            {
                var setPolicyResponse = await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
                {
                    BucketName = "test-bucket",
                    Policy = """
                         {
                             "Version": "2012-10-17",
                             "Statement": [
                                 {
                                     "Sid": "PublicReadGetObject",
                                     "Effect": "Allow",
                                     "Principal": "*",
                                     "Action": "s3:GetObject",
                                     "Resource": "arn:aws:s3:::test-bucket/*"
                                 }
                             ]
                         }
                         """
                });
            }

            var corsPolicy = new CORSRule
            {
                AllowedHeaders = new List<string> { "*" },
                AllowedMethods = new List<string> { "GET", "PUT", "POST" },
                AllowedOrigins = new List<string> { "*" },
                MaxAgeSeconds = 3000
            };
            var corsRequest = new PutCORSConfigurationRequest
            {
                BucketName = "test-bucket",
                Configuration = new CORSConfiguration
                {
                    Rules = [corsPolicy]
                }
            };
            await s3Client.PutCORSConfigurationAsync(corsRequest, cts.Token);
        }
        catch (DistributedApplicationException)
        {
            AspireFailed = true;
            serviceUrl = "http://localhost:4566";
            dbConnectionString = "Host=localhost;Username=postgres;Password=postgres;Database=filesdb";
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        _tempDir1 = new TempDir();
        _tempDir2 = new TempDir();
        _bucketPrefix = Guid.NewGuid().ToString("N");
        string cdnUrl = $"{serviceUrl}/test-bucket/";
        Factory = new LocalRepoFactory(_tempDir1.Path, _tempDir2.Path, serviceUrl, cdnUrl, _bucketPrefix, dbConnectionString);
        Client = Factory.CreateClient();
        var csrfTokenResponse = await Client.PostAsync("/api/csrf/token", null);
        var csrfTokenResult = await csrfTokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        if (csrfTokenResult?.Token is not null)
        {
            Client.DefaultRequestHeaders.Add("X-CSRF-Token", csrfTokenResult.Token);
        }
    }

    public void Dispose()
    {
        _browser?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _playwright?.Dispose();
        Factory?.Dispose();
        _tempDir1?.Dispose();
        _tempDir2?.Dispose();
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
