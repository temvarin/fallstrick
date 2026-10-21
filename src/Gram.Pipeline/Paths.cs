namespace Gram.Pipeline;

/// <summary>Where the pipeline reads its sources and writes the app's data, relative to the repository root.</summary>
public sealed record Paths(string Root)
{
    public string Sources => Path.Combine(Root, "content");
    public string AppData => Path.Combine(Root, "src", "Gram.App", "wwwroot", "data");

    /// <summary>Walks up from <paramref name="start"/> to the repository root: the folder that contains <c>content</c> and <c>Gram.slnx</c>.</summary>
    public static Paths FindFrom(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Gram.slnx")) && Directory.Exists(Path.Combine(dir.FullName, "content")))
                return new Paths(dir.FullName);
        }
        throw new DirectoryNotFoundException($"No repository root (with Gram.slnx and content/) above {start}.");
    }
}
