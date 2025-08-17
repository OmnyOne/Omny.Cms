using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Omny.Cms.Builder.Services;
using Omny.Cms.Rendering.ContentRendering;
using NUnit.Framework;
using Omny.Cms.Files;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Hexo;
using Omny.Cms.Editor;

namespace Omny.Cms.Builder.Tests;

public class ContentBuilderTests
{
    [Test]
    public async Task BuildDefaultSite_GeneratesExpectedHtml()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var sampleSite = Path.Combine(repoRoot, "SampleSite");
        var tempDir = Path.Combine(Path.GetTempPath(), "omnytest_" + Guid.NewGuid().ToString());
        CopyDirectory(sampleSite, tempDir);

        ServiceCollection services = new ServiceCollection();
        
        services.AddBuilderServices();
        // add logging
        services.AddLogging(builder => builder.AddConsole());
        
        var provider = services.BuildServiceProvider();
        if (provider.GetRequiredService<IFileSystem>() is LocalFileSystem fs)
        {
            fs.BasePath = tempDir;
        }
        var builder = provider.GetRequiredService<ContentBuilder>();
        await builder.BuildAsync(tempDir);

        var outputDir = Path.Combine(tempDir, "output");
        var indexPath = Path.Combine(outputDir, "index.html");
        var aboutPath = Path.Combine(outputDir, "about.html");

        Assert.IsTrue(File.Exists(indexPath));
        Assert.IsTrue(File.Exists(aboutPath));

        var indexExpected = File.ReadAllText(Path.Combine(tempDir, "content/pages/index/Body.html"));
        var aboutExpected = File.ReadAllText(Path.Combine(tempDir, "content/pages/about/Body.html"));
        var indexActual = File.ReadAllText(indexPath);
        var aboutActual = File.ReadAllText(aboutPath);
        StringAssert.Contains("<html><body><ul", indexActual);
        StringAssert.Contains(indexExpected.Trim(), indexActual);
        StringAssert.Contains(aboutExpected.Trim(), aboutActual);

        var imagePath = Path.Combine(outputDir, "pixel.png");
        Assert.IsTrue(File.Exists(imagePath));
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }
}
