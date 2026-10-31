using Microsoft.Extensions.Hosting;
using Omny.Cms.Builder;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureOmnyBuilder();

return await builder.RunOmnyBuilder(args);
