namespace AiChatExport.Tests;

public class EndToEndTests
{
    [Fact]
    public void Help_exits_zero_and_readme_names_the_tool()
    {
        var stdout = new StringWriter();
        var previous = Console.Out;
        Console.SetOut(stdout);
        int code;
        try
        {
            code = Exporter.Run(["--help"]);
        }
        finally
        {
            Console.SetOut(previous);
        }

        Assert.Equal(0, code);
        var help = stdout.ToString();
        Assert.Contains("usage: ai-chat-export", help, StringComparison.Ordinal);
        Assert.Contains("terminal-dark", help, StringComparison.Ordinal);
        Assert.Contains("terminal-light", help, StringComparison.Ordinal);
        Assert.Contains("dark-web", help, StringComparison.Ordinal);
        Assert.Contains("light-web", help, StringComparison.Ordinal);
        Assert.Contains("--markdown", help, StringComparison.Ordinal);
        Assert.Contains("--projects-root", help, StringComparison.Ordinal);
        Assert.Contains("--source", help, StringComparison.Ordinal);
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));
        Assert.StartsWith("# ai-chat-export\n", readme);
        Assert.Contains("`--markdown` writes the Markdown file", readme, StringComparison.Ordinal);
        Assert.Contains("terminal-dark", readme, StringComparison.Ordinal);
        Assert.Contains("terminal-light", readme, StringComparison.Ordinal);
        Assert.Contains("dark-web", readme, StringComparison.Ordinal);
        Assert.Contains("light-web", readme, StringComparison.Ordinal);
        Assert.Contains("Up and Down", readme, StringComparison.Ordinal);
        Assert.Contains("Left and Right", readme, StringComparison.Ordinal);
        Assert.Contains("Enter asks for a file name", readme, StringComparison.Ordinal);
        Assert.Contains("Esc closes", readme, StringComparison.Ordinal);
        Assert.Contains("`i` shows the selected chat id", readme, StringComparison.Ordinal);
        Assert.Contains("Cu-CLI", readme, StringComparison.Ordinal);
        Assert.Contains("Cu-IDE", readme, StringComparison.Ordinal);
        Assert.Contains("~/.cursor/projects", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Built_executable_exports_one_html_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        try
        {
            var (messages, prompts) = Support.SampleMessages();
            var chats = Path.Combine(root, "chats");
            Support.WriteChat(chats, "demo", root, "Fantasy Map Maker", messages: messages, prompts: prompts);
            var outDir = Path.Combine(root, "out");
            var exe = Path.Combine(RepoRoot(), "src", "AiChatExport", "bin", "Debug", "net10.0", "ai-chat-export.exe");
            Assert.True(File.Exists(exe), exe);
            var stdout = new StringWriter();
            var projects = Path.Combine(root, "no-projects");
            var code = Exporter.Run(["--id", "demo", "--chats-root", chats, "--projects-root", projects, "--out", outDir]);
            Assert.Equal(0, code);
            var htmlPath = Path.Combine(outDir, "Fantasy Map Maker.html");
            var page = File.ReadAllText(htmlPath);
            Assert.Contains("Please add a river tool.", page, StringComparison.Ordinal);
            Assert.Contains("The river tool lives in tools/river.py.", page, StringComparison.Ordinal);
            Assert.DoesNotContain("http", page, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(outDir, "Fantasy Map Maker.md")));

            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = exe;
            process.StartInfo.ArgumentList.Add("--id");
            process.StartInfo.ArgumentList.Add("demo");
            process.StartInfo.ArgumentList.Add("--chats-root");
            process.StartInfo.ArgumentList.Add(chats);
            process.StartInfo.ArgumentList.Add("--projects-root");
            process.StartInfo.ArgumentList.Add(projects);
            process.StartInfo.ArgumentList.Add("--out");
            process.StartInfo.ArgumentList.Add(Path.Combine(root, "exe-out"));
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            var exeOut = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
            var exeHtml = Path.Combine(root, "exe-out", "Fantasy Map Maker.html");
            Assert.True(File.Exists(exeHtml));
            Assert.Contains(Path.GetFullPath(exeHtml), exeOut, StringComparison.Ordinal);
            _ = stdout;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiChatExport.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("solution root");
    }
}
