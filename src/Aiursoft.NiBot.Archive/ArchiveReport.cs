using System.Net;
using System.Text;
using System.Text.Json;

namespace Aiursoft.NiBot.Archive;

public static class ArchiveReport
{
    public static ArchivePlan Read(string path)
    {
        ArchiveFiles.AssertNoLinks(path);
        return JsonSerializer.Deserialize<ArchivePlan>(File.ReadAllText(path))
               ?? throw new InvalidDataException("Empty archive plan.");
    }

    public static void Write(string path, ArchivePlan plan)
    {
        path = Path.GetFullPath(path);
        var htmlPath = path + ".html";
        ValidateOutput(path, plan);
        ValidateOutput(htmlPath, plan);
        if (File.Exists(path) || File.Exists(htmlPath))
            throw new IOException("Report already exists. Choose a new --report path.");
        ArchiveFiles.WriteJson(path, plan, overwrite: false);
        using var stream = new FileStream(htmlPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(Render(plan));
    }

    public static void ValidateOutput(string path, ArchivePlan plan)
    {
        path = Path.GetFullPath(path);
        ArchiveFiles.AssertNoLinks(path);
        if (ArchiveFiles.IsWithin(path, plan.SourceRoot) || ArchiveFiles.IsWithin(path, plan.DestinationRoot))
            throw new ArgumentException("Reports must be outside the source and destination image directories.");
    }

    public static string Render(ArchivePlan plan)
    {
        static string E(string text) => WebUtility.HtmlEncode(text);
        static string Link(string path) => E(new Uri(Path.GetFullPath(path)).AbsoluteUri);
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>NiBot archive preview</title>");
        html.Append("<style>body{font:16px system-ui;margin:2rem;max-width:1200px}article{border-top:1px solid #bbb;padding:1rem 0}img{max-width:160px;max-height:180px;object-fit:contain}li{margin:.5rem 0}code{overflow-wrap:anywhere}small{display:block} .candidates{display:flex;gap:1rem;flex-wrap:wrap}</style>");
        html.Append("<h1>Incremental archive preview</h1><p>Only Ready items are copied by archive-apply. Review items remain in the source. Scores are similarities, not probabilities.</p>");
        html.Append("<p>").Append(E(string.Join(" · ", plan.Items.GroupBy(i => i.Decision).Select(g => $"{g.Key}: {g.Count()}")))).Append("</p>");
        foreach (var item in plan.Items)
        {
            var source = ArchiveFiles.Resolve(plan.SourceRoot, item.Source);
            html.Append("<article><h2>").Append(E(item.Decision.ToString())).Append(" — ").Append(E(item.Source)).Append("</h2>");
            html.Append("<a href=\"").Append(Link(source)).Append("\"><img loading=\"lazy\" src=\"").Append(Link(source)).Append("\" alt=\"Source\"></a>");
            html.Append("<p>").Append(E(item.Reason)).Append("</p>");
            if (item.DuplicateOf != null)
                html.Append("<p>Duplicate candidate: <a href=\"").Append(Link(item.DuplicateOf)).Append("\"><img loading=\"lazy\" src=\"")
                    .Append(Link(item.DuplicateOf)).Append("\" alt=\"Duplicate candidate\">").Append(E(item.DuplicateOf)).Append("</a></p>");
            html.Append("<div class=\"candidates\">");
            foreach (var candidate in item.Candidates)
            {
                html.Append("<section><h3>").Append(E(candidate.Folder)).Append("</h3><p>")
                    .Append(E($"Similarity {candidate.Similarity:F3}; neighbor support {candidate.Support:P0}")).Append("</p>");
                foreach (var example in candidate.Examples)
                {
                    var path = ArchiveFiles.Resolve(plan.DestinationRoot, example);
                    html.Append("<a href=\"").Append(Link(path)).Append("\"><img loading=\"lazy\" src=\"").Append(Link(path))
                        .Append("\" alt=\"").Append(E(example)).Append("\"></a>");
                }
                html.Append("</section>");
            }
            html.Append("</div></article>");
        }
        return html.Append("</html>").ToString();
    }
}
