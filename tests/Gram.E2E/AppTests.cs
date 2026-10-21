using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace Gram.E2E;

[Collection(UsesPublishedApp.Name)]
[Trait("Category", "E2E")]
public class AppTests(PublishedApp app)
{
    /// <summary>Waits until Blazor has started: the start page shows its title.</summary>
    private static async Task OpenAsync(IPage page, string path = "")
    {
        await page.GotoAsync(path);
        await page.Locator(".topbar h1").Filter(new() { HasNotText = "Fallstrick" }).WaitForAsync();
    }

    /// <summary>
    /// Answers every card until the session ends: a new card gets the first option (and another one on a retry) or "Lösung
    /// zeigen"; a card that comes back gets the answer its feedback showed, as a learner would.
    /// </summary>
    private static async Task PractiseToTheEndAsync(IPage page)
    {
        var learned = new Dictionary<string, string>();
        for (var step = 0; step < 300; step++)
        {
            if (await page.GetByText("Geschafft").IsVisibleAsync()) return;
            var prompt = await PromptKeyAsync(page);
            var next = page.Locator(".btn.next");
            if (await next.IsVisibleAsync())
            {
                learned[prompt] = await page.Locator(".gap.filled").InnerTextAsync();
                await next.ClickAsync();
            }
            else if (await page.Locator("form.typed").IsVisibleAsync())
            {
                if (learned.TryGetValue(prompt, out var typed))
                {
                    await page.Locator("form.typed input").FillAsync(typed);
                    await page.GetByText("Prüfen").ClickAsync();
                }
                else await page.GetByText("Lösung zeigen").ClickAsync();
            }
            else
            {
                var options = page.Locator(".answer:not([disabled])");
                var known = learned.TryGetValue(prompt, out var answer)
                    ? options.Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^-?{answer}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })
                    : null;
                await (known is not null && await known.CountAsync() > 0 ? known : options).First.ClickAsync();
            }
        }
        Assert.Fail("The session did not end.");
    }

    /// <summary>The prompt as it reads while the gap is still open, so it matches the card when it comes back.</summary>
    private static Task<string> PromptKeyAsync(IPage page) =>
        page.Locator(".prompt").EvaluateAsync<string>("""
            p => { const c = p.cloneNode(true); c.querySelectorAll('.ru-line').forEach(e => e.remove());
                   const g = c.querySelector('.gap'); if (g) g.textContent = '_'; return c.textContent.replace(/\s+/g, ' ').trim(); }
            """);

    [Fact]
    public async Task A_session_can_be_practised_to_the_end()
    {
        var page = await app.NewPageAsync();
        await OpenAsync(page);
        await page.GetByText("Üben starten").ClickAsync();

        await PractiseToTheEndAsync(page);

        await page.GetByText("Zur Übersicht").ClickAsync();
        await Assertions.Expect(page.Locator(".area").First).ToContainTextAsync("geübt");
        Assert.DoesNotContain(" 0 / ", await page.Locator(".areas").InnerTextAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tapping_the_noun_of_an_article_card_shows_its_meaning_but_not_its_article()
    {
        var page = await app.NewPageAsync();
        await OpenAsync(page, "ueben");
        for (var i = 0; i < 20 && !await page.Locator(".word").IsVisibleAsync(); i++)
            await PassAsync(page);

        await page.Locator(".word").ClickAsync();

        await Assertions.Expect(page.Locator(".prompt .ru-line")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".prompt .gap")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("filled"));
    }

    [Fact]
    public async Task The_app_works_offline_after_the_first_visit()
    {
        var page = await app.NewPageAsync();
        await OpenAsync(page);
        // The service worker has copied the whole release into its cache once it controls the page.
        await page.EvaluateAsync("navigator.serviceWorker.ready");
        await page.ReloadAsync();
        await page.WaitForFunctionAsync("navigator.serviceWorker.controller !== null");

        await page.Context.SetOfflineAsync(true);
        await OpenAsync(page, "regeln");

        await Assertions.Expect(page.Locator("table.decl").First).ToContainTextAsync("dem");
    }

    [Fact]
    public async Task A_backup_brings_the_progress_back()
    {
        var page = await app.NewPageAsync();
        await OpenAsync(page, "ueben");
        await PractiseToTheEndAsync(page);

        await OpenAsync(page, "mehr");
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByText("Sicherung speichern").ClickAsync());
        var file = await download.PathAsync();

        await page.EvaluateAsync("localStorage.clear()");
        await OpenAsync(page, "mehr");
        await page.Locator("input[type=file]").SetInputFilesAsync(file);
        await page.GetByRole(AriaRole.Button, new() { Name = "Ersetzen" }).ClickAsync();

        await OpenAsync(page);
        Assert.DoesNotContain(" 0 / ", await page.Locator(".areas").InnerTextAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ueben")]
    [InlineData("regeln")]
    [InlineData("woerter")]
    [InlineData("mehr")]
    public async Task Every_page_passes_the_accessibility_check(string path)
    {
        var page = await app.NewPageAsync();
        await OpenAsync(page, path);
        if (path == "ueben") await PassAsync(page); // also check the feedback state

        var result = await page.RunAxe(new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
        });

        var serious = result.Violations.Where(v => v.Impact is "serious" or "critical")
            .Select(v => $"{v.Id}: {v.Help} ({string.Join(", ", v.Nodes.Select(n => n.Target))})");
        Assert.Empty(serious);
    }

    /// <summary>Answers the current card wrongly or gives up, leaving it on its feedback screen; clicks Weiter first if a feedback screen is open.</summary>
    private static async Task PassAsync(IPage page)
    {
        var next = page.Locator(".btn.next");
        if (await next.IsVisibleAsync()) { await next.ClickAsync(); return; }
        if (await page.Locator("form.typed").IsVisibleAsync()) { await page.GetByText("Lösung zeigen").ClickAsync(); return; }
        while (!await next.IsVisibleAsync()) await page.Locator(".answer:not([disabled])").First.ClickAsync();
    }
}
