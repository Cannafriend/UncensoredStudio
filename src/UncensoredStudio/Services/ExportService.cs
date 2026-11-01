using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UncensoredStudio.Models;

namespace UncensoredStudio.Services
{
    public class ExportService
    {
        private readonly string _exportDir = @"I:\UncensoredAI\exports";

        public ExportService()
        {
            if (!Directory.Exists(_exportDir))
            {
                Directory.CreateDirectory(_exportDir);
            }
        }

        public async Task<string> ExportChapterToMarkdownAsync(Project project, Chapter chapter)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {project.Title}");
            sb.AppendLine($"## {chapter.Title}");
            sb.AppendLine($"*Genre: {project.Genre} | Word Count: {chapter.WordCount} | Exported: {DateTime.Now:yyyy-MM-dd HH:mm}*");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(chapter.Content);

            string safeFileName = SanitizeFileName($"{project.Title}_{chapter.Title}.md");
            string filePath = Path.Combine(_exportDir, safeFileName);
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        public async Task<string> ExportEntireNovelToMarkdownAsync(Project project, System.Collections.Generic.List<Chapter> chapters)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {project.Title}");
            sb.AppendLine($"*Genre: {project.Genre}*");
            if (!string.IsNullOrWhiteSpace(project.Synopsis))
            {
                sb.AppendLine();
                sb.AppendLine($"**Synopsis:** {project.Synopsis}");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var chap in chapters)
            {
                sb.AppendLine($"## {chap.Title}");
                sb.AppendLine();
                sb.AppendLine(chap.Content);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            string safeFileName = SanitizeFileName($"{project.Title}_Full_Novel.md");
            string filePath = Path.Combine(_exportDir, safeFileName);
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName.Replace(' ', '_');
        }
    }
}
