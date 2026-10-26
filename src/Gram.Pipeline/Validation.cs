using System.Text.RegularExpressions;
using Gram.Domain;
using Gram.Domain.Grammar;

namespace Gram.Pipeline;

/// <summary>Checks the sources before anything is written: every problem with where it is, so content errors never reach the app.</summary>
internal static partial class Validation
{
    // A preposition fused with the article: "am" stands for "an dem".
    static readonly Dictionary<string, string> Contractions = new()
    {
        ["am"] = "an", ["ans"] = "an", ["im"] = "in", ["ins"] = "in", ["aufs"] = "auf", ["ums"] = "um",
        ["vom"] = "von", ["zum"] = "zu", ["zur"] = "zu", ["beim"] = "bei", ["fürs"] = "für", ["übers"] = "über",
    };

    static readonly string[] CaseArticles = ["der", "ein", "kein"];

    // Prepositions that always take the same case; two-way prepositions (an, auf, in, über …) aren't listed.
    static readonly Dictionary<string, Case> FixedCase = new()
    {
        ["mit"] = Case.Dative, ["bei"] = Case.Dative, ["von"] = Case.Dative, ["zu"] = Case.Dative, ["aus"] = Case.Dative,
        ["nach"] = Case.Dative, ["seit"] = Case.Dative, ["für"] = Case.Accusative, ["durch"] = Case.Accusative,
        ["gegen"] = Case.Accusative, ["ohne"] = Case.Accusative, ["um"] = Case.Accusative,
        ["wegen"] = Case.Genitive, ["trotz"] = Case.Genitive, ["während"] = Case.Genitive,
    };

    public static List<string> Check(SourceData src)
    {
        var problems = new List<string>();
        void Sources(string where, string[] ids)
        {
            if (ids.Length == 0) problems.Add($"{where}: no source in src");
            foreach (var id in ids.Where(id => !src.Sources.ContainsKey(id))) problems.Add($"{where}: unknown source \"{id}\"");
        }

        foreach (var n in src.Nouns)
        {
            var where = $"nouns.json {n.De}";
            if (Codes.Gender(n.G) is null) problems.Add($"{where}: gender \"{n.G}\" is not m, n or f");
            if (Codes.Level(n.Level) is null) problems.Add($"{where}: unknown level \"{n.Level}\"");
            if (string.IsNullOrWhiteSpace(n.Ru)) problems.Add($"{where}: no Russian translation");
            if (n.Hint?.Contains(n.De, StringComparison.OrdinalIgnoreCase) == true) problems.Add($"{where}: the hint contains the noun itself");
            Sources(where, n.Src);
        }
        foreach (var same in src.Nouns.GroupBy(n => n.De).Where(g => g.Count() > 1))
        {
            if (same.Select(n => n.G).Distinct().Count() < same.Count()) problems.Add($"nouns.json {same.Key}: listed twice with the same gender");
            if (same.Any(n => string.IsNullOrWhiteSpace(n.Hint))) problems.Add($"nouns.json {same.Key}: has two genders, so each needs a hint");
        }

        foreach (var r in src.Rektion)
        {
            var where = $"rektion.json {r.Id}";
            if (Codes.Case(r.Case) is not (Case.Accusative or Case.Dative or Case.Genitive)) problems.Add($"{where}: case \"{r.Case}\" is not akk, dat or gen");
            if (Codes.Level(r.Level) is null) problems.Add($"{where}: unknown level \"{r.Level}\"");
            if (string.IsNullOrWhiteSpace(r.Ru)) problems.Add($"{where}: no Russian translation");
            if (Regex.IsMatch(r.Hint, $@"\b{Regex.Escape(r.Prep)}\b", RegexOptions.IgnoreCase)) problems.Add($"{where}: the hint gives away \"{r.Prep}\"");
            if (r.Sentences.Length == 0) problems.Add($"{where}: no sentences");
            if (FixedCase.TryGetValue(r.Prep, out var fixedCase) && Codes.Case(r.Case) is { } given && given != fixedCase)
                problems.Add($"{where}: {r.Prep} always takes the {German.Case(fixedCase)}");
            Sources(where, r.Src);
            foreach (var sentence in r.Sentences)
            {
                var gap = GapText.Parse(sentence);
                if (gap is null || gap.Answers.Count == 0) { problems.Add($"{where}: \"{sentence}\" needs exactly one {{answer}}"); continue; }
                foreach (var answer in gap.Answers)
                {
                    var first = answer.Split(' ')[0].ToLowerInvariant();
                    if (first != r.Prep && Contractions.GetValueOrDefault(first) != r.Prep)
                        problems.Add($"{where}: answer \"{answer}\" doesn't start with \"{r.Prep}\"");
                }
            }
        }
        foreach (var id in src.Rektion.GroupBy(r => r.Id).Where(g => g.Count() > 1)) problems.Add($"rektion.json: id \"{id.Key}\" is used twice");
        foreach (var group in src.Rektion.Where(r => r.Group is not null).GroupBy(r => r.Group).Where(g => g.Count() < 2))
            problems.Add($"rektion.json: group \"{group.Key}\" has only one entry");

        var nounForms = KnownNounForms(src.Nouns);
        foreach (var f in src.Frames)
        {
            var where = $"frames.json \"{f.Text}\"";
            var gap = GapText.Parse(f.Text);
            if (gap is null || gap.Answers.Count > 0) problems.Add($"{where}: needs exactly one empty {{}}");
            if (Codes.Case(f.Case) is not { } @case) { problems.Add($"{where}: unknown case \"{f.Case}\""); continue; }
            if (Codes.Column(f.G) is not { } column) { problems.Add($"{where}: gender \"{f.G}\" is not m, n, f or pl"); continue; }
            if (!Determiners.TryGet(f.Det, out var determiner)) { problems.Add($"{where}: unknown determiner \"{f.Det}\""); continue; }
            if (determiner.Form(@case, column) is null) problems.Add($"{where}: \"{f.Det}\" has no form for {f.Case} {f.G}");
            if (f.Adj is null && !CaseArticles.Contains(f.Det)) problems.Add($"{where}: case practice uses der, ein or kein, not \"{f.Det}\"");
            if (f.Adj is not null && Adjective.Stem(f.Adj).Length == 0) problems.Add($"{where}: empty adjective");

            // "why" names the case at its end ("mit + Dativ", "Subjekt → Nominativ"), and a fixed-case preposition must agree.
            if (!f.Why.EndsWith(German.Case(@case), StringComparison.Ordinal)) problems.Add($"{where}: \"{f.Why}\" doesn't end with {German.Case(@case)}");
            if (FixedCase.TryGetValue(f.Why.Split(' ')[0], out var prepCase) && prepCase != @case)
                problems.Add($"{where}: {f.Why.Split(' ')[0]} always takes the {German.Case(prepCase)}");

            // The noun right after the gap, where the noun list knows it, must have the frame's gender or number.
            if (gap is not null && FirstWord().Match(gap.After) is { Success: true } noun && nounForms.TryGetValue(noun.Value, out var forms)
                && !forms.Contains(column))
                problems.Add($"{where}: \"{noun.Value}\" is {string.Join(" or ", forms.Select(German.Column))}, not {German.Column(column)}");
        }

        foreach (var w in src.WhoWhom)
        {
            if (w.Answer < 0 || w.Answer >= w.Options.Length) problems.Add($"wer-wen.json {w.Id}: answer {w.Answer} is not one of the {w.Options.Length} options");
        }
        foreach (var id in src.WhoWhom.GroupBy(w => w.Id).Where(g => g.Count() > 1)) problems.Add($"wer-wen.json: id \"{id.Key}\" is used twice");

        foreach (var rule in src.GenderRules)
        {
            if (Codes.Gender(rule.G) is null) problems.Add($"gender-rules.json -{rule.Suffix}: gender \"{rule.G}\" is not m, n or f");
            Sources($"gender-rules.json -{rule.Suffix}", rule.Src);
        }

        foreach (var term in src.Glossary.Where(t => string.IsNullOrWhiteSpace(t.Ru))) problems.Add($"glossary.json {term.De}: no Russian translation");

        return problems;
    }

    /// <summary>Every form of every listed noun, with the columns it can stand for (<see cref="NounForms"/>).</summary>
    static Dictionary<string, HashSet<GenderNumber>> KnownNounForms(IEnumerable<SourceNoun> nouns)
    {
        var forms = new Dictionary<string, HashSet<GenderNumber>>(StringComparer.Ordinal);
        foreach (var n in nouns)
        {
            if (Codes.Gender(n.G) is not { } gender) continue;
            foreach (var (form, column) in NounForms.Of(n.De, gender, n.Pl))
                (forms.TryGetValue(form, out var set) ? set : forms[form] = []).Add(column);
        }
        return forms;
    }

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex FirstWord();
}
