using Microsoft.Extensions.Hosting;
using Omny.Cms.Builder;

var builder = Host.CreateApplicationBuilder(args);
builder.ConfigureOmnyBuilder();

return await builder.RunOmnyBuilder(args);
