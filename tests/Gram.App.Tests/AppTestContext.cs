using Gram.App.Services;
using Gram.Domain;
using Gram.Domain.Grammar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Gram.App.Tests;

/// <summary>The app's real services, with a catalog the test chooses, progress in memory and a clock the test controls.</summary>
public abstract class AppTestContext : BunitContext
{
    protected static readonly DateOnly Today = new(2026, 10, 20);

    protected static Segment[] Text(string text) => [new Segment(text)];

    protected static readonly Noun Messer = new("Messer", Gender.Neuter, "Messer", null, "нож", Level.A2);

    /// <summary>"besteht" can be tapped for its translation.</summary>
    protected static readonly RektionEntry BestehenAus = new(
        "bestehen-aus", "bestehen", "aus", Case.Dative, "sich zusammensetzen aus", "Es geht um Bestandteile.", "состоять из", Level.B1,
        [new Gap([new("Die Wohnung "), new("besteht", 0), new(" ")], ["aus"], Text(" drei Zimmern."))]);

    protected static readonly CaseFrame HelfenDem = new(Text("Ich helfe "), Text(" Mann."), "der", Case.Dative, GenderNumber.Masculine, "helfen + Dativ");

    protected static Catalog Catalog(IReadOnlyList<Noun>? nouns = null, IReadOnlyList<RektionEntry>? rektion = null, IReadOnlyList<CaseFrame>? frames = null) =>
        new(nouns ?? [], rektion ?? [], frames ?? [], [], [], [], [new Term("bestehen", "состоять")]);

    protected FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));

    /// <summary>Call before rendering: the app works with exactly this catalog.</summary>
    protected void Use(Catalog catalog)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddAppServices(catalog);
        Services.AddSingleton<IKeyValueStore, MemoryStore>();
        Services.AddSingleton<TimeProvider>(Clock);
        Services.AddSingleton(new Random(42));
    }

    protected Progress Progress => Services.GetRequiredService<ProgressService>().Progress;
    protected Dialogs Dialogs => Services.GetRequiredService<Dialogs>();

    protected static void Choose(IRenderedComponent<WithTopBar> page, string option) =>
        page.FindAll(".answer").Single(b => b.TextContent.Trim() == option).Click();

    private sealed class MemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> _data = [];
        public string? Read(string key) => _data.GetValueOrDefault(key);
        public void Write(string key, string value) => _data[key] = value;
    }
}
