using NUnit.Framework;
using Microsoft.Playwright;
using System;

namespace Omny.Cms.Ui.Tests;

[SetUpFixture]
public class PlaywrightGlobalSetup
{
    [OneTimeSetUp]
    public void InstallPlaywright()
    {
        int exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "--with-deps","chromium" });
        if (exitCode != 0)
        {
            throw new Exception($"Playwright exited with code {exitCode}");
        }
    }
}

