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
    public void Terminal_dark_is_default_and_the_other_themes_stay_distinct()
    {
        var page = Page(_transcript);
        var darkWeb = Page(_transcript, "dark-web");
        var lightWeb = Page(_transcript, "light-web");
        var terminalLight = Page(_transcript, "terminal-light");
        var unknown = Page(_transcript, "nope");
        Assert.Contains("#071207", page, StringComparison.Ordinal);
        Assert.Contains("font: 18px/1.5 Consolas, ui-monospace, monospace;", page, StringComparison.Ordinal);
        Assert.Contains("font-family: Consolas, ui-monospace, monospace;", page, StringComparison.Ordinal);
        Assert.Contains("border-radius: 0;", page, StringComparison.Ordinal);
        Assert.Contains("--user: #12301a;", page, StringComparison.Ordinal);
        Assert.Contains("--agent: #0b1610;", page, StringComparison.Ordinal);
        Assert.Contains("--mark: #e2c15a;", page, StringComparison.Ordinal);
        Assert.DoesNotContain("#161616", page, StringComparison.Ordinal);
        Assert.DoesNotContain("#f7f7f4", page, StringComparison.Ordinal);
        Assert.Equal(page, unknown);
        Assert.Contains("#161616", darkWeb, StringComparison.Ordinal);
        Assert.Contains("font: 18px/1.5 Georgia, \"Iowan Old Style\", Palatino, serif;", darkWeb, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Segoe UI\", system-ui, sans-serif;", darkWeb, StringComparison.Ordinal);
        Assert.Contains("border-radius: 8px;", darkWeb, StringComparison.Ordinal);
        Assert.Contains("--mark: #e0b040;", darkWeb, StringComparison.Ordinal);
        Assert.Contains("--user: #2c261c;", darkWeb, StringComparison.Ordinal);
        Assert.Contains("--agent: #1e2428;", darkWeb, StringComparison.Ordinal);
        Assert.Contains("#f7f7f4", lightWeb, StringComparison.Ordinal);
        Assert.Contains("--user: #f6efe4;", lightWeb, StringComparison.Ordinal);
        Assert.Contains("--agent: #f3f5f7;", lightWeb, StringComparison.Ordinal);
        Assert.DoesNotContain("#161616", lightWeb, StringComparison.Ordinal);
        Assert.Contains("#e6f5df", terminalLight, StringComparison.Ordinal);
        Assert.Contains("--user: #d7f0c8;", terminalLight, StringComparison.Ordinal);
        Assert.Contains("--agent: #e7f3e2;", terminalLight, StringComparison.Ordinal);
        Assert.Contains("--mark: #7a5b12;", terminalLight, StringComparison.Ordinal);
        Assert.Contains("border-radius: 0;", terminalLight, StringComparison.Ordinal);
        foreach (var rendered in new[] { page, darkWeb, lightWeb, terminalLight })
        {
            Assert.DoesNotContain("<script", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("http", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Please add a river tool.", rendered, StringComparison.Ordinal);
            Assert.Contains("The river tool lives in tools/river.py.", rendered, StringComparison.Ordinal);
            Assert.Contains("section.you { background: var(--user); border-left: 3px solid var(--mark); scroll-margin-top: 5rem; }", rendered, StringComparison.Ordinal);
            Assert.Contains("section.agent { background: var(--agent); }", rendered, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void User_and_agent_panels_differ_and_navigator_is_30rem()
    {
        var transcript = new Transcript();
        transcript.Blocks.Add(new Block("you", text: "Prompt"));
        transcript.Blocks.Add(new Block("agent", text: "Reply"));
        transcript.Blocks.Add(new Block("system", text: "Note"));
        var page = Page(transcript, "dark-web");
        Assert.Contains("section.system, section.context { background: var(--panel); }", page, StringComparison.Ordinal);
        Assert.Contains("<section class=\"you\"", page, StringComparison.Ordinal);
        Assert.Contains("<section class=\"agent\" data-turn=\"1\">", page, StringComparison.Ordinal);
        Assert.Contains("<section class=\"system\">", page, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 70rem) {", page, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 30rem minmax(0, 1fr);", page, StringComparison.Ordinal);
        Assert.Contains("nav .time { font-size: 10px; line-height: 1; white-space: nowrap; }", page, StringComparison.Ordinal);
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
    public void Turn_mark_groups_the_user_and_following_agent()
    {
        var transcript = new Transcript();
        transcript.Blocks.Add(new Block("system", text: "Preamble"));
        transcript.Blocks.Add(new Block("you", text: "First"));
        transcript.Blocks.Add(new Block("agent", text: "Reply one"));
        transcript.Blocks.Add(new Block("context", text: "Injected"));
        transcript.Blocks.Add(new Block("agent", text: "Reply two"));
        transcript.Blocks.Add(new Block("you", text: "Second"));
        transcript.Blocks.Add(new Block("you", text: "Third"));
        transcript.Blocks.Add(new Block("agent", text: "Reply three"));
        var article = Page(transcript).Split("<article>", 2)[1];
        Assert.Contains("<section class=\"you\" id=\"user-1\" data-turn=\"1\">", article, StringComparison.Ordinal);
        Assert.Equal(3, Count(article, "data-turn=\"1\""));
        Assert.Contains("<section class=\"you\" id=\"user-2\" data-turn=\"2\">", article, StringComparison.Ordinal);
        Assert.Equal(1, Count(article, "data-turn=\"2\""));
        Assert.Contains("<section class=\"you\" id=\"user-3\" data-turn=\"3\">", article, StringComparison.Ordinal);
        Assert.Contains("<section class=\"agent\" data-turn=\"3\">", article, StringComparison.Ordinal);
        Assert.Equal(2, Count(article, "data-turn=\"3\""));
        Assert.Contains("<section class=\"system\">", article, StringComparison.Ordinal);
        Assert.Contains("<section class=\"context\">", article, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"system\" data-turn", article, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"context\" data-turn", article, StringComparison.Ordinal);
    }

    [Fact]
    public void Checkbox_filter_keeps_the_link_and_restores_from_either_end()
    {
        var transcript = new Transcript();
        transcript.Blocks.Add(new Block("you", text: "First line\nLater line"));
        transcript.Blocks.Add(new Block("agent", text: "Reply"));
        transcript.Blocks.Add(new Block("you", text: "Second"));
        transcript.Blocks.Add(new Block("you", text: "Third"));
        transcript.Footer.Add("Kept note");
        var page = Page(transcript, "dark-web");
        var nav = page.Split("<nav>", 2)[1].Split("</nav>", 2)[0];
        var item = nav.Split("<li>", 2)[1].Split("</li>", 2)[0];
        Assert.True(item.IndexOf("type=\"checkbox\"", StringComparison.Ordinal) < item.IndexOf("<a ", StringComparison.Ordinal));
        Assert.Contains("id=\"keep-1\"", item, StringComparison.Ordinal);
        Assert.Contains("href=\"#user-1\"", item, StringComparison.Ordinal);
        Assert.DoesNotContain("<input", item.Split("<a ", 2)[1], StringComparison.Ordinal);
        Assert.DoesNotMatch("<input[^>]*\\bchecked\\b", page);
        Assert.Contains("<a href=\"#user-1\">", nav, StringComparison.Ordinal);
        const string button = "<button type=\"reset\">Clear filter</button>";
        Assert.Equal(2, Count(nav, button));
        Assert.True(nav.IndexOf(button, StringComparison.Ordinal) < nav.IndexOf("<ol>", StringComparison.Ordinal));
        Assert.True(nav.LastIndexOf(button, StringComparison.Ordinal) > nav.IndexOf("</ol>", StringComparison.Ordinal));
        Assert.Contains("nav button { display: none; font: inherit; color: var(--text); background: var(--bg); border: 1px solid var(--rule); }", page, StringComparison.Ordinal);
        Assert.Contains("body:has(nav input:checked) nav button { display: block; }", page, StringComparison.Ordinal);
        Assert.Contains("nav form > button:first-child { position: sticky; top: 0; z-index: 1; }", page, StringComparison.Ordinal);
        const string hide = "body:has(nav input:checked) article section { display: none; }";
        Assert.Contains(hide, page, StringComparison.Ordinal);
        Assert.DoesNotContain("header", hide, StringComparison.Ordinal);
        Assert.DoesNotContain("footer", hide, StringComparison.Ordinal);
        for (var number = 1; number <= 3; number++)
        {
            Assert.Contains($"body:has(#keep-{number}:checked) article [data-turn=\"{number}\"] {{ display: block; }}", page, StringComparison.Ordinal);
        }

        Assert.Contains("<header>", page, StringComparison.Ordinal);
        Assert.Contains("<h1>Fantasy Map Maker</h1>", page, StringComparison.Ordinal);
        Assert.Contains("<footer>", page, StringComparison.Ordinal);
        Assert.Contains("Kept note", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", page, StringComparison.OrdinalIgnoreCase);

        var markdown = MarkdownRenderer.Render(transcript, _title, _workspace, _timestamp);
        Assert.DoesNotContain("checkbox", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Clear filter", markdown, StringComparison.Ordinal);
        Assert.Contains("## User", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigator_time_sits_closer_to_its_prompt_than_to_the_next_entry()
    {
        var transcript = new Transcript();
        transcript.Blocks.Add(new Block("you", text: "First", timestamp: "Oct 23, 2026 11:13 AM"));
        transcript.Blocks.Add(new Block("you", text: "Second"));
        var page = Page(transcript);
        Assert.Contains("nav a { display: flex; flex-direction: column; gap: 0; min-width: 0; color: var(--text); text-decoration: none; }", page, StringComparison.Ordinal);
        Assert.Contains("nav li + li { margin-top: 0.85rem; }", page, StringComparison.Ordinal);
        Assert.Contains("nav .time { font-size: 10px; line-height: 1; white-space: nowrap; }", page, StringComparison.Ordinal);
        Assert.DoesNotContain("0.75rem", page, StringComparison.Ordinal);
        var markdown = MarkdownRenderer.Render(transcript, _title, _workspace, _timestamp);
        Assert.DoesNotContain("0.85rem", markdown, StringComparison.Ordinal);
        Assert.Contains("## User · Oct 23, 2026 11:13 AM", markdown, StringComparison.Ordinal);
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

    private string Page(Transcript transcript, string theme = "terminal-dark")
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
