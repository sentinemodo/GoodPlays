namespace GoodPlays.Tests;

public class DotEnvLoaderTests
{
    [Fact]
    public void TryLoad_SetsVariablesWithoutOverwritingExisting()
    {
        var root = Path.Combine(Path.GetTempPath(), "goodplays-dotenv-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var envPath = Path.Combine(root, ".env");
        File.WriteAllText(envPath, """
            GOODPLAYS_DOTENV_TEST_A=from-file
            GOODPLAYS_DOTENV_TEST_B=secret-from-file
            """);

        Environment.SetEnvironmentVariable("GOODPLAYS_DOTENV_TEST_A", "already-set");

        try
        {
            var loaded = GoodPlays.Infrastructure.Configuration.DotEnvLoader.TryLoad(root);
            Assert.True(loaded);
            Assert.Equal("already-set", Environment.GetEnvironmentVariable("GOODPLAYS_DOTENV_TEST_A"));
            Assert.Equal("secret-from-file", Environment.GetEnvironmentVariable("GOODPLAYS_DOTENV_TEST_B"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOODPLAYS_DOTENV_TEST_A", null);
            Environment.SetEnvironmentVariable("GOODPLAYS_DOTENV_TEST_B", null);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindEnvFile_WalksUpDirectoryTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "goodplays-dotenv-" + Guid.NewGuid());
        var nested = Path.Combine(root, "src", "GoodPlays.Api");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(root, ".env"), "KEY=value");

        try
        {
            var found = GoodPlays.Infrastructure.Configuration.DotEnvLoader.FindEnvFile(nested, ".env");
            Assert.Equal(Path.Combine(root, ".env"), found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
