var builder = DistributedApplication.CreateBuilder(args);

var localStack = builder
    .AddContainer("localstack", "localstack/localstack", "latest")
    .WithEnvironment("SERVICES", "s3,sqs") // choose needed LocalStack services
    .WithEnvironment("DEBUG", "1")
    .WithEnvironment("HOSTNAME_EXTERNAL", "localhost")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHttpEndpoint(port: 4566, targetPort: 4566, name: "localstack", isProxied: false);

var postgres = builder.AddPostgres("postgres");
var filesDb = postgres.AddDatabase("filesdb");

var app = builder.Build();
await app.RunAsync();
