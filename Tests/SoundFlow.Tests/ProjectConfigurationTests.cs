using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SoundFlow.Interfaces;

namespace SoundFlow.Tests;

public class ProjectConfigurationTests
{
    [Fact]
    public void GlobalJsonUsesValidSdkFeatureBand()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var version = document.RootElement.GetProperty("sdk").GetProperty("version").GetString();

        Assert.Matches(@"^\d+\.\d+\.[1-9]\d{2}$", version);
    }

    [Theory]
    [InlineData("Src/SoundFlow.targets")]
    [InlineData("Codecs/SoundFlow.Codecs.FFMpeg/SoundFlow.Codecs.FFMpeg.targets")]
    [InlineData("Extensions/SoundFlow.Extensions.WebRtc.Apm/SoundFlow.Extensions.WebRtc.Apm.targets")]
    [InlineData("Midi/SoundFlow.Midi.PortMidi/SoundFlow.Midi.PortMidi.targets")]
    public void RuntimeTargetsPointToExistingAssets(string relativeTargetsPath)
    {
        var root = FindRepositoryRoot();
        var targetsPath = Path.Combine(root, relativeTargetsPath.Replace('/', Path.DirectorySeparatorChar));
        var document = XDocument.Load(targetsPath);

        foreach (var runtimeTarget in document.Descendants("RuntimeTargets"))
        {
            var include = runtimeTarget.Attribute("Include")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(include));

            Assert.DoesNotContain("..", include);

            var assetPath = ResolveRuntimeAssetPath(root, relativeTargetsPath, include!)
                .Replace('\\', Path.DirectorySeparatorChar);
            assetPath = Path.GetFullPath(assetPath);

            Assert.True(File.Exists(assetPath) || Directory.Exists(assetPath), $"Missing runtime asset: {assetPath}");
        }
    }

    [Fact]
    public void RuntimeTargetsUseCanonicalIosRid()
    {
        var root = FindRepositoryRoot();
        var targetFiles = new[]
        {
            "Src/SoundFlow.targets",
            "Codecs/SoundFlow.Codecs.FFMpeg/SoundFlow.Codecs.FFMpeg.targets",
            "Extensions/SoundFlow.Extensions.WebRtc.Apm/SoundFlow.Extensions.WebRtc.Apm.targets"
        };

        foreach (var file in targetFiles)
        {
            var text = File.ReadAllText(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));
            Assert.DoesNotContain("iOS-arm64", text);
            Assert.Contains("ios-arm64", text);
        }
    }

    [Fact]
    public void PositionChangedEventArgsPreservesLongPositions()
    {
        const long position = (long)int.MaxValue + 42;

        var args = new PositionChangedEventArgs(position);

        Assert.Equal(position, args.NewPosition);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SoundFlow.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ResolveRuntimeAssetPath(string root, string relativeTargetsPath, string include)
    {
        var runtimeRoot = relativeTargetsPath switch
        {
            "Src/SoundFlow.targets" => Path.Combine(root, "Src", "Backends", "MiniAudio", "runtimes"),
            "Codecs/SoundFlow.Codecs.FFMpeg/SoundFlow.Codecs.FFMpeg.targets" => Path.Combine(root, "Codecs", "SoundFlow.Codecs.FFMpeg", "runtimes"),
            "Extensions/SoundFlow.Extensions.WebRtc.Apm/SoundFlow.Extensions.WebRtc.Apm.targets" => Path.Combine(root, "Extensions", "SoundFlow.Extensions.WebRtc.Apm", "runtimes"),
            "Midi/SoundFlow.Midi.PortMidi/SoundFlow.Midi.PortMidi.targets" => Path.Combine(root, "Midi", "SoundFlow.Midi.PortMidi", "runtimes"),
            _ => throw new ArgumentOutOfRangeException(nameof(relativeTargetsPath), relativeTargetsPath, null)
        };

        const string marker = "$(MSBuildThisFileDirectory)runtimes";
        Assert.StartsWith(marker, include);
        return runtimeRoot + include[marker.Length..];
    }
}
