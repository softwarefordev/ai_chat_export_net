using System.Text.Json.Nodes;

namespace AiChatExport.Tests;

public class TranscriptTests
{
    private static Transcript Build(IEnumerable<JsonObject> messages, IReadOnlyList<string> prompts, bool verbose = false, bool showTools = false, bool showDetail = false)
        => TranscriptBuilder.Build(messages, prompts, verbose, showTools, showDetail);

    [Fact]
    public void Query_portion_omits_markup_and_injected_context()
    {
        var query = "Please add a river tool.";
        var wrapped = $"<timestamp>Friday, Oct 23, 2026, 11:13 AM (UTC-7)</timestamp>\n<user_query>\n{query}\n</user_query>";
        var transcript = Build(
            [Support.Message("user", new JsonArray { Support.TextPart("text", "INJECTED_CONTEXT_BLOCK"), Support.TextPart("text", wrapped) })],
            [query]);
        var you = transcript.Blocks.Single(block => block.Kind == "you");
        var visible = string.Join("\n", transcript.Blocks.Select(block => block.Text));
        Assert.Equal(query, you.Text);
        Assert.DoesNotContain("<timestamp>", visible);
        Assert.DoesNotContain("<user_query>", visible);
        Assert.DoesNotContain("INJECTED_CONTEXT_BLOCK", visible);
    }

    [Fact]
    public void Plain_prompt_without_elements_is_the_query()
    {
        var query = "Please add a river tool.";
        var transcript = Build([Support.Message("user", query)], [query]);
        Assert.Equal(query, transcript.Blocks.Single(block => block.Kind == "you").Text);
    }

    [Fact]
    public void Summary_only_message_is_omitted()
    {
        var summary = "Earlier turns covered the exporter.";
        var transcript = Build(
            [Support.Message("user", $"<conversation_summary>\n{summary}\n</conversation_summary>")],
            []);
        Assert.Empty(transcript.Blocks);
    }

    [Fact]
    public void Summary_only_appears_with_detail()
    {
        var summary = "Earlier turns covered the exporter.";
        var messages = new[] { Support.Message("user", $"<conversation_summary>\n{summary}\n</conversation_summary>") };
        Assert.Empty(Build(messages, []).Blocks);
        var shown = TranscriptBuilder.Build(messages, [], showDetail: true);
        var you = shown.Blocks.Single(block => block.Kind == "you");
        Assert.Equal("", you.Text);
        Assert.Equal(summary, you.Detail);
    }

    [Fact]
    public void Default_omits_command_blocks_and_keeps_one_agent_stretch()
    {
        var query = "Please add a river tool.";
        var messages = new[]
        {
            Support.Message("system", "SYSTEM_PROMPT_TEXT"),
            Support.Message("user", new JsonArray
            {
                Support.TextPart("text", "INJECTED_CONTEXT_BLOCK\n<cursor_commands>\nopsx-propose\n</cursor_commands>"),
                Support.TextPart("text", $"<user_query>{query}</user_query>"),
            }),
            Support.Message("assistant", new JsonArray
            {
                Support.TextPart("reasoning", "REASONING_PRIVATE"),
                Support.TextPart("text", "Visible answer."),
                Support.ToolCall("Shell", new JsonObject { ["command"] = "SECRET_COMMAND_XYZ", ["description"] = "List map tool modules" }),
            }),
            Support.Message("tool", new JsonArray
            {
                new JsonObject { ["type"] = "tool-result", ["toolName"] = "Shell", ["result"] = "TOOL_RESULT_PAYLOAD" },
            }),
        };
        var transcript = Build(messages, [query]);
        Assert.Equal(["you", "agent"], transcript.Blocks.Select(block => block.Kind).ToList());
        var visible = string.Join("\n", transcript.Blocks.Select(block => block.Text + block.Detail + string.Join("\n", block.Captions)));
        Assert.Contains(query, visible);
        Assert.DoesNotContain("opsx-propose", visible);
        Assert.DoesNotContain("SYSTEM_PROMPT_TEXT", visible);
        Assert.DoesNotContain("REASONING_PRIVATE", visible);
        Assert.DoesNotContain("TOOL_RESULT_PAYLOAD", visible);
    }

    [Fact]
    public void Several_assistant_messages_share_one_heading_until_the_next_prompt()
    {
        var messages = new[]
        {
            Support.Message("user", "First prompt"),
            Support.Message("assistant", new JsonArray { Support.TextPart("text", "First answer") }),
            Support.Message("assistant", new JsonArray { Support.TextPart("text", "Second answer") }),
            Support.Message("user", "Second prompt"),
            Support.Message("assistant", new JsonArray { Support.TextPart("text", "Third answer") }),
        };
        var transcript = Build(messages, ["First prompt", "Second prompt"]);
        Assert.Equal(2, transcript.Blocks.Count(block => block.Kind == "agent"));
    }

    [Fact]
    public void Captions_omit_command_text_including_a_command_without_a_description()
    {
        var (messages, prompts) = Support.SampleMessages();
        var transcript = TranscriptBuilder.Build(messages, prompts, showTools: true);
        var captions = transcript.Blocks.SelectMany(block => block.Captions).ToList();
        Assert.Equal(["Read tools/river.py", "Glob *.py", "Ran List map tool modules"], captions);
        Assert.DoesNotContain("SECRET_COMMAND_XYZ", string.Join("\n", captions));
        Assert.Equal(
            "Ran command",
            TranscriptBuilder.Caption(new JsonObject
            {
                ["toolName"] = "Shell",
                ["args"] = new JsonObject { ["command"] = "SECRET_COMMAND_XYZ" },
            }));
    }

    [Fact]
    public void Detail_and_verbose_show_a_command_block_once()
    {
        var query = "Please add a river tool.";
        var command = "opsx-propose user turns";
        var summary = "Earlier turns covered the exporter.";
        var messages = new[]
        {
            Support.Message("user", new JsonArray
            {
                Support.TextPart("text", "INJECTED_CONTEXT_BLOCK\n"
                    + $"<conversation_summary>\n{summary}\n</conversation_summary>\n"
                    + $"<cursor_commands>\n{command}\n</cursor_commands>"),
                Support.TextPart("text", $"<user_query>{query}</user_query>"),
            }),
        };
        var transcript = TranscriptBuilder.Build(messages, [query], verbose: true, showDetail: true);
        var you = transcript.Blocks.Single(block => block.Kind == "you");
        var context = transcript.Blocks.Single(block => block.Kind == "context");
        Assert.Equal($"{summary}\n\n{command}", you.Detail);
        Assert.Contains("INJECTED_CONTEXT_BLOCK", context.Text);
        Assert.DoesNotContain(summary, context.Text);
        Assert.DoesNotContain(command, context.Text);
        var shown = you.Text + "\n" + you.Detail + "\n" + context.Text;
        Assert.Equal(1, Count(shown, summary));
        Assert.Equal(1, Count(shown, command));
    }

    [Fact]
    public void Turn_time_uses_catalog_style_on_user_and_agent()
    {
        var query = "Please add a river tool.";
        var messages = new[]
        {
            Support.Message("user", $"<timestamp>Friday, Oct 23, 2026, 11:13 AM (UTC-7)</timestamp>\n<user_query>{query}</user_query>"),
            Support.Message("assistant", new JsonArray { Support.TextPart("text", "Done.") }),
        };
        var transcript = Build(messages, [query]);
        Assert.Equal("Oct 23, 2026 11:13 AM", transcript.Blocks[0].Timestamp);
        Assert.Equal("Oct 23, 2026 11:13 AM", transcript.Blocks[1].Timestamp);
    }

    [Fact]
    public void Impossible_day_stays_raw_and_odd_parts_are_skipped()
    {
        var stamp = "Friday, Oct 32, 2026, 11:13 AM (UTC-7)";
        var messages = new JsonObject[]
        {
            new()
            {
                ["role"] = 4,
                ["content"] = "SECRET_ROLE",
            },
            Support.Message("assistant", new JsonArray
            {
                new JsonObject
                {
                    ["type"] = 1,
                    ["text"] = "HIDDEN_NUMERIC",
                },
                Support.TextPart("text", "Visible reply"),
            }),
            Support.Message("user", $"<timestamp>{stamp}</timestamp>\n<user_query>Hello there</user_query>"),
        };
        var transcript = Build(messages, ["Hello there"]);
        Assert.Equal(stamp, transcript.Blocks.Single(block => block.Kind == "you").Timestamp);
        var body = string.Join("\n", transcript.Blocks.Select(block => block.Text));
        Assert.Contains("Visible reply", body, StringComparison.Ordinal);
        Assert.Contains("Hello there", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_ROLE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("HIDDEN_NUMERIC", body, StringComparison.Ordinal);
        Assert.Empty(transcript.Footer);
    }

    [Fact]
    public void Footer_names_skipped_blob_and_unlinked_image()
    {
        var (messages, prompts) = Support.SampleMessages();
        var root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "store.db");
            Support.WriteDb(dbPath, messages, [Support.Png], ["not-json-blob"u8.ToArray()]);
            var conversation = Store.Load(dbPath);
            var transcript = TranscriptBuilder.Build(
                conversation.Messages,
                prompts,
                skipped: conversation.Skipped,
                unlinkedImages: conversation.UnlinkedImages);
            var footer = string.Join("\n", transcript.Footer);
            Assert.Single(conversation.Skipped);
            Assert.Single(conversation.UnlinkedImages);
            Assert.Contains(conversation.Skipped[0], footer);
            Assert.Contains(conversation.UnlinkedImages[0], footer);
            var body = string.Join("\n", transcript.Blocks.Select(block => block.Text));
            Assert.DoesNotContain(conversation.UnlinkedImages[0], body);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
