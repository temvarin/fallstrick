using Gram.Pipeline;

var paths = Paths.FindFrom(Directory.GetCurrentDirectory());

return args switch
{
    ["build"] => Commands.Build(paths),
    ["check"] => Commands.Check(paths),
    ["words"] => Commands.Words(paths),
    ["verify", .. var rest] => await Commands.VerifyAsync(paths, rest is ["--goethe", var folder] ? folder : null),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("""
        Usage: dotnet run --project src/Gram.Pipeline -- <command>

          build   validate content/ and write the app's wwwroot/data/catalog.json
          check   validate and verify wwwroot/data is up to date (CI)
          words   list words in sentences that can't be tapped for a translation yet
          verify [--goethe <folder>]
                  compare nouns with Wiktionary (gender, plural) and, given the DWDS copies of
                  the Goethe lists (A1.csv, A2.csv, B1.csv), with the list each noun cites
        """);
    return 2;
}
