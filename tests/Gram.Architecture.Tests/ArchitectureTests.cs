using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Gram.App.Services;
using Gram.Domain;
using NetArchTest.Rules;

namespace Gram.Architecture.Tests;

/// <summary>
/// The structure the code must keep, checked on the compiled code: which part may use which. A rule written only in a
/// README erodes one convenient call at a time. Each rule says why it exists. Some rules are kept by the compiler
/// instead: the project references (the domain references nothing, the pipeline not the app), card keys can only be made by
/// <see cref="CardKey"/>, and progress can only change through <see cref="ProgressService"/>, which saves every change
/// (their constructor and changing methods are internal).
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Catalog).Assembly;
    private static readonly Assembly App = typeof(Dialogs).Assembly;

    [Fact]
    public void The_domain_is_plain_code_without_ui_network_or_files() => Holds(
        Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(
            "Microsoft.AspNetCore", "Microsoft.JSInterop", "System.Net", "System.IO", "Gram.App", "Gram.Pipeline"),
        "Gram.Domain runs in the browser and in the pipeline, and its tests need no fakes: it computes, and leaves "
        + "I/O to the app and the pipeline (README, Architecture).");

    [Fact]
    public void The_grammar_engine_knows_nothing_of_cards_exercises_or_progress()
    {
        var rest = Domain.GetTypes().Where(t => t.Namespace == "Gram.Domain" && t.IsPublic).Select(t => t.FullName!).ToArray();

        Holds(
            Types.InAssembly(Domain).That().ResideInNamespaceMatching(Within("Gram.Domain.Grammar")).ShouldNot().HaveDependencyOnAny(rest),
            "Gram.Domain.Grammar is the German grammar itself (ADR 2): the paradigms are tested cell by cell against Duden "
            + "and stay true whatever the app does with them. Cards, exercises and scheduling build on it, never the other way.");
    }

    [Fact]
    public void The_ui_shows_grammar_only_through_exercises_and_tables() => Holds(
        Ui().ShouldNot().HaveDependencyOnAny(
            "Gram.Domain.Grammar.Determiner", "Gram.Domain.Grammar.Adjective", "Gram.Domain.Grammar.NounForms"),
        "An exercise knows its answer, its table and how the adjective inflects; a page that computes forms itself can "
        + "disagree with it. Tables come as Paradigm (review, finding 2).");

    [Fact]
    public void The_ui_leaves_scheduling_to_the_session() => Holds(
        Ui().ShouldNot().HaveDependencyOnAny("Gram.Domain.Fsrs"),
        "Grading and scheduling are domain decisions (ADR 3): the page passes an Attempt's rating to the Session, "
        + "which decides what the scheduler sees.");

    [Fact]
    public void The_app_s_services_know_nothing_of_pages_and_components() => Holds(
        Types.InAssembly(App).That().ResideInNamespaceMatching(Within("Gram.App.Services"))
            .ShouldNot().HaveDependencyOnAny("Gram.App.Pages", "Gram.App.Components", "Gram.App.MainLayout"),
        "Dependencies point from the UI to the services, never back: services are tested without rendering anything.");

    [Fact]
    public void Components_know_nothing_of_pages() => Holds(
        Types.InAssembly(App).That().ResideInNamespaceMatching(Within("Gram.App.Components"))
            .ShouldNot().HaveDependencyOnAny("Gram.App.Pages"),
        "Components are the parts pages are built from; a component that knows a page can't be used on another.");

    [Fact]
    public void The_ui_reaches_storage_network_and_javascript_only_through_services() => Holds(
        Ui().ShouldNot().HaveDependencyOnAny(
            "Gram.Domain.IKeyValueStore", "Gram.Domain.ProgressStore", "Gram.App.Services.LocalStorage", "System.Net.Http",
            "Microsoft.JSInterop"),
        "Pages and components get storage and the browser from services, which tests replace with fakes "
        + "(AppTestContext). Browser features go into Services/Browser.cs and wwwroot/browser.js.");

    [Fact]
    public void The_domain_s_state_changes_only_through_its_methods()
    {
        // Records may have init: they are values, set once. Internal and private setters are for the class itself and the JSON source generator.
        var settable = Domain.GetExportedTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } set
                    && !set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                .Select(p => $"{type.FullName}.{p.Name}"));

        Assert.True(!settable.Any(),
            "Progress, Session and Attempt keep their rules (one answer counts, new cards per day) only if state changes "
            + $"through their methods. Public setters: {string.Join(", ", settable)}");
    }

    private static PredicateList Ui() => Types.InAssembly(App).That()
        .ResideInNamespaceMatching(Within("Gram.App.Pages")).Or()
        .ResideInNamespaceMatching(Within("Gram.App.Components")).Or()
        .ResideInNamespaceMatching(@"^Gram\.App$"); // the layout, the router and the load error page

    // The namespace and the ones below it.
    private static string Within(string ns) => $@"^{Regex.Escape(ns)}(\..+)?$";

    private static void Holds(ConditionList rule, string why)
    {
        var result = rule.GetResult();
        Assert.True(result.IsSuccessful, $"{why}\nBroken by: {string.Join(", ", Breaking(result))}");
    }

    // The types that break a rule. Lambdas and async methods compile to nested types: they count as the type they're in.
    private static string[] Breaking(TestResult result) =>
        [.. (result.FailingTypes ?? []).Select(t => t.FullName.Split('/', '+')[0]).Distinct().Order(StringComparer.Ordinal)];
}
