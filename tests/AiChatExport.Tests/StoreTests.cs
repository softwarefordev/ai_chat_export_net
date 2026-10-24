using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace AiChatExport.Tests;

public class StoreTests
{
    [Fact]
    public void Messages_stay_in_order_without_checkpoint_duplicate_and_bytes_stay()
    {
        var (messages, _) = Support.SampleMessages();
        var root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "store.db");
            Support.WriteDb(dbPath, messages, [], ["not-json-blob"u8.ToArray()]);
            var before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dbPath)));
            var conversation = Store.Load(dbPath);
            var after = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dbPath)));
            Assert.Equal(before, after);
            Assert.Equal(
                ["system", "user", "assistant", "tool"],
                conversation.Messages.Select(message => message["role"]!.GetValue<string>()).ToList());
            Assert.Single(conversation.Skipped);
            Assert.Empty(conversation.UnlinkedImages);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Protobuf_checkpoints_precede_the_live_tail_once()
    {
        var early = Support.Message("user", "EARLY_TURN");
        var reply = Support.Message("assistant", "EARLY_REPLY");
        var mid = Support.Message("user", "MID_TURN");
        var late = Support.Message("user", "LATE_TURN");
        var summary = "<summary>CHECKPOINT_SUMMARY_TEXT</summary>"u8.ToArray();
        var root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "store.db");
            Support.WriteProtobufDb(
                dbPath,
                [([early, reply], summary), ([early, mid], summary)],
                [late]);
            var conversation = Store.Load(dbPath);
            var texts = conversation.Messages.Select(message => message["content"]!.GetValue<string>()).ToList();
            Assert.Equal(["EARLY_TURN", "EARLY_REPLY", "MID_TURN", "LATE_TURN"], texts);
            Assert.Equal(1, texts.Count(text => text == "EARLY_TURN"));
            Assert.DoesNotContain("CHECKPOINT_SUMMARY_TEXT", string.Join("\n", texts));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Protobuf_footer_skips_checkpoints_and_side_blobs_only()
    {
        var early = Support.Message("user", "EARLY_TURN");
        var late = Support.Message("user", "LATE_TURN");
        var root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "store.db");
            var ids = Support.WriteProtobufDb(
                dbPath,
                [([early], "<summary>CHECKPOINT_SUMMARY_TEXT</summary>"u8.ToArray())],
                [late],
                [(8, "# Tasks\nfile body"u8.ToArray()), (3, "\n\u0005specs"u8.ToArray())],
                ["mystery-bytes-not-a-turn"u8.ToArray()]);
            var conversation = Store.Load(dbPath);
            var footer = string.Join("\n", conversation.Skipped);
            Assert.DoesNotContain(ids["checkpoint_ids"][0], footer);
            Assert.DoesNotContain(ids["side_ids"][0], footer);
            Assert.DoesNotContain(ids["side_ids"][1], footer);
            Assert.Contains(ids["unknown_ids"][0], conversation.Skipped);
            var texts = conversation.Messages.Select(message => message["content"]!.GetValue<string>()).ToList();
            Assert.Equal(["EARLY_TURN", "LATE_TURN"], texts);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
