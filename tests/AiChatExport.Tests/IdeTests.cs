using System.Text.Json.Nodes;

namespace AiChatExport.Tests;

public class IdeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));

    public IdeTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Turn_ended_keeps_one_agent_heading_and_other_lines_go_to_the_footer()
    {
        var folder = Path.Combine(_root, "agent-transcripts", "room");
        Directory.CreateDirectory(folder);
        var lines = new[]
        {
            User("River map"),
            Assistant(new JsonArray { Text("First reply") }),
            """{"type":"turn_ended","status":"success"}""",
            Assistant(new JsonArray
            {
                Text("Second reply"),
                new JsonObject
                {
                    ["type"] = "tool_use",
                    ["name"] = "Shell",
                    ["input"] = new JsonObject
                    {
                        ["command"] = "secret-command-text",
                        ["description"] = "List the map files",
                    },
                },
            }),
            """{"type":"note","text":"NOT-IN-BODY"}""",
        };
        File.WriteAllText(Path.Combine(folder, "room.jsonl"), string.Join('\n', lines) + "\n");

        var conversation = IdeTranscript.Load(folder);
        var transcript = TranscriptBuilder.Build(
            conversation.Messages,
            [],
            showTools: true,
            skipped: conversation.Skipped);
        var agents = transcript.Blocks.Where(block => block.Kind == "agent").ToList();
        Assert.Single(agents);
        Assert.Contains("First reply", agents[0].Text, StringComparison.Ordinal);
        Assert.Contains("Second reply", agents[0].Text, StringComparison.Ordinal);
        Assert.Equal(["Ran List the map files"], agents[0].Captions);
        Assert.DoesNotContain("secret-command-text", agents[0].Captions[0], StringComparison.Ordinal);
        var body = string.Join('\n', transcript.Blocks.Select(block => block.Text));
        Assert.DoesNotContain("NOT-IN-BODY", body, StringComparison.Ordinal);
        Assert.DoesNotContain("turn_ended", body, StringComparison.Ordinal);
        Assert.Contains("Skipped unrecognized data line 5", string.Join('\n', transcript.Footer), StringComparison.Ordinal);
    }

    private static string User(string query) =>
        new JsonObject
        {
            ["role"] = "user",
            ["message"] = new JsonObject
            {
                ["content"] = new JsonArray { Text($"<user_query>\n{query}\n</user_query>") },
            },
        }.ToJsonString();

    private static string Assistant(JsonArray content) =>
        new JsonObject
        {
            ["role"] = "assistant",
            ["message"] = new JsonObject { ["content"] = content },
        }.ToJsonString();

    private static JsonObject Text(string text) => new() { ["type"] = "text", ["text"] = text };
}
