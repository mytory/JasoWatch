using JasoWatch;
using Xunit;

namespace JasoWatch.Tests;

public sealed class FilenameNormalizerTests
{
    [Fact]
    public void EnablesWindowsAutostartByDefault() => Assert.True(AppSettings.Defaults().StartWithWindows);

    [Fact]
    public void DetectsAndNormalizesUnicodeNfc() => Assert.True(FilenameNormalizer.NeedsNormalization("한글.pdf"));

    [Fact]
    public void DoesNotTreatCompatibilityJamoAsNfd() => Assert.False(FilenameNormalizer.NeedsNormalization("ㅎㅏㄴㄱㅡㄹ.pdf"));

    [Theory]
    [InlineData("download.crdownload", true)]
    [InlineData("notes.part.pdf", false)]
    [InlineData("~$draft.docx", true)]
    public void AppliesTemporaryFileRules(string name, bool expected)
    {
        var settings = AppSettings.Defaults();
        Assert.Equal(expected, FilenameNormalizer.IsExcludedFile(name, settings));
    }

    [Fact]
    public void ChoosesSmallestCollisionNumber()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "한글.pdf", "한글 1.pdf", "한글 3.pdf" };
        Assert.Equal("한글 2.pdf", FilenameNormalizer.FindAvailableName("C:\\unused", "한글.pdf", used.Contains));
    }

    [Fact]
    public void HonorsDirectChildScope() => Assert.True(FilenameNormalizer.IsWithinScope("C:\\watch\\file.txt", "C:\\watch", false));

    [Fact]
    public void ExcludesNestedPathWhenSubfoldersDisabled() => Assert.False(FilenameNormalizer.IsWithinScope("C:\\watch\\nested\\file.txt", "C:\\watch", false));

    [Fact]
    public async Task QueuedFileIsRenamedToNfc()
    {
        var folder = Path.Combine(Path.GetTempPath(), "JasoWatchTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var original = Path.Combine(folder, "한글.txt");
            await File.WriteAllTextAsync(original, "test");
            using var service = new FileNormalizationService(() => new AppSettings { WatchFolder = folder });
            service.Queue(original);
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.True(File.Exists(Path.Combine(folder, "한글.txt")));
        }
        finally { Directory.Delete(folder, true); }
    }
}
