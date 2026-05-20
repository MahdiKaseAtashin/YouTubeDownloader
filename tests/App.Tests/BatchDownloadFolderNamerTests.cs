using App.Application.Services;
using Xunit;

namespace App.Tests;

public sealed class BatchDownloadFolderNamerTests
{
    [Fact]
    public void Create_uses_index_and_title()
    {
        var name = BatchDownloadFolderNamer.Create(1, "How to get the V shape", "PSSVbHX5w90");
        Assert.Equal("1-how-to-get-the-v-shape", name);
    }

    [Fact]
    public void Create_strips_invalid_path_characters()
    {
        var name = BatchDownloadFolderNamer.Create(2, "Test: A/B?*", "abc12345678");
        Assert.Equal("2-test-a-b", name);
    }
}
