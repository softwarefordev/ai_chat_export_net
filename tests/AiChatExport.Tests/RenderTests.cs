using System.Text.Json.Nodes;

namespace AiChatExport.Tests;

public class RenderTests
{
    private readonly Transcript _transcript;
    private readonly string _title = "Fantasy Map Maker";
    private readonly string _workspace = "SoggyMapMaker";
    private readonly string _timestamp = "Aug 15, 2026 04:42 PM";

    public RenderTests()
    {
        var (messages, prompts) = Support.SampleMessages();
        _transcript = TranscriptBuilder.Build(messages, prompts);
    }

    [Fact]
    public void Assistant_subset_renders_fences_lists_paragraphs_and_inline_code()
    {
        Assert.Contains("<pre><code>print(1)</code></pre>", HtmlRenderer.RenderAssistant("```\nprint(1)\n```"), StringComparison.Ordinal);
        Assert.Contains("<li>alpha</li>", HtmlRenderer.RenderAssistant("- alpha\n- beta"), StringComparison.Ordinal);
        Assert.Contains("<li>beta</li>", HtmlRenderer.RenderAssistant("- alpha\n- beta"), StringComparison.Ordinal);
        Assert.Contains("<p>Hello there</p>", HtmlRenderer.RenderAssistant("Hello there"), StringComparison.Ordinal);
        var inline = HtmlRenderer.RenderAssistant("use `x` here");
        Assert.Contains("<code>x</code>", inline, StringComparison.Ordinal);
        Assert.Contains("use ", inline, StringComparison.Ordinal);
    }

    [Fact]
    public void Dark_is_default_and_light_is_separate_with_no_script_or_external_address()
    {
        var dark = Page(_transcript);
        var light = Page(_transcript, "light");
        Assert.Contains("#161616", dark, StringComparison.Ordinal);
        Assert.DoesNotContain("#f7f7f4", dark, StringComparison.Ordinal);
        Assert.Contains("#f7f7f4", light, StringComparison.Ordinal);
        Assert.DoesNotContain("#161616", light, StringComparison.Ordinal);
        foreach (var page in new[] { dark, light })
        {
            Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("http", page, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Please add a river tool.", page, StringComparison.Ordinal);
            Assert.Contains("The river tool lives in tools/river.py.", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void User_and_agent_panels_differ_and_navigator_is_30rem()
    {
        var transcript = new Transcript();
        transcript.Blocks.Add(new Block("you", text: "Prompt"));
        transcript.Blocks.Add(new Block("agent", text: "Reply"));
        transcript.Blocks.Add(new Block("system", text: "Note"));
        var dark = Page(transcript);
        var light = Page(transcript, "light");
        Assert.Contains("--user: #2c261c;", dark, StringComparison.Ordinal);
        Assert.Contains("--agent: #1e2428;", dark, StringComparison.Ordinal);
        Assert.Contains("--user: #f6efe4;", light, StringComparison.Ordinal);
        Assert.Contains("--agent: #f3f5f7;", light, StringComparison.Ordinal);
        Assert.Contains("section.you { background: var(--user); border-left: 3px solid var(--mark); scroll-margin-top: 5rem; }", dark, StringComparison.Ordinal);
        Assert.Contains("section.agent { background: var(--agent); }", dark, StringComparison.Ordinal);
        Assert.Contains("section.system, section.context { background: var(--panel); }", dark, StringComparison.Ordinal);
        Assert.Contains("<section class=\"you\"", dark, StringComparison.Ordinal);
        Assert.Contains("<section class=\"agent\">", dark, StringComparison.Ordinal);
        Assert.Contains("<section class=\"system\">", dark, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 70rem) {", dark, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 30rem minmax(0, 1fr);", dark, StringComparison.Ordinal);
        Assert.Contains("nav .time { font-size: 10px; white-space: nowrap; }", dark, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Segoe UI\", system-ui, sans-serif;", dark, StringComparison.Ordinal);
        Assert.Contains("font: 18px/1.5 Georgia, \"Iowan Old Style\", Palatino, serif;", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigator_links_user_turns_and_compacts_times()
    {
        var cases = new (string Stamp, string Compact)[]
        {
            ("Oct 23, 2026 01:08 PM", "26-10-23 13:08"),
            ("Oct 23, 2026 11:13 AM", "26-10-23 11:13"),
            ("Sep 1, 2026 09:05 AM", "26-09-01 09:05"),
            ("Oct 23, 2026 12:00 AM", "26-10-23 00:00"),
            ("Oct 23, 2026 12:00 PM", "26-10-23 12:00"),
            ("sometime next week", "sometime next week"),
        };
        var transcript = new Transcript();
        foreach (var (stamp, _) in cases)
        {
            transcript.Blocks.Add(new Block("you", text: "Query", timestamp: stamp));
        }

        transcript.Blocks.Add(new Block("agent", text: "Reply", timestamp: "Oct 23, 2026 01:08 PM"));
        var page = Page(transcript);
        var nav = page.Split("<article>", 2)[0];
        var article = page.Split("<article>", 2)[1];
        foreach (var (stamp, compact) in cases)
        {
            Assert.Contains($"<span class=\"time\">{compact}</span>", nav, StringComparison.Ordinal);
            Assert.Contains($"<h2>User <span class=\"time\">{stamp}</span></h2>", article, StringComparison.Ordinal);
        }

        Assert.Contains("text-overflow: ellipsis", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
        var image = new Transcript();
        image.Blocks.Add(new Block("you", text: "See this", images: [("image/png", "AAAA")]));
        Assert.Contains("data:image/png;base64,AAAA", HtmlRenderer.Render(image, "T", "W", "t"), StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_matches_visible_turns_without_theme_colors()
    {
        var (messages, prompts) = Support.SampleMessages();
        var shown = TranscriptBuilder.Build(messages, prompts, showTools: true);
        var page = Page(shown);
        var markdown = MarkdownRenderer.Render(shown, _title, _workspace, _timestamp);
        foreach (var text in new[] { "Please add a river tool.", "The river tool lives in tools/river.py.", "Read tools/river.py", "Ran List map tool modules" })
        {
            Assert.Contains(text, page, StringComparison.Ordinal);
            Assert.Contains(text, markdown, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("#161616", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("#f7f7f4", markdown, StringComparison.Ordinal);
        Assert.Equal(1, Count(page, "<h2>Agent</h2>"));
        Assert.Equal(1, Count(markdown, "## Agent"));
    }

    private string Page(Transcript transcript, string theme = "dark")
        => HtmlRenderer.Render(transcript, _title, _workspace, _timestamp, theme);

    private static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
