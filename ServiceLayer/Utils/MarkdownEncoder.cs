using Markdig;
using ReverseMarkdown;
using System.Text.RegularExpressions;

namespace ServiceLayer.Utils
{
    public static class MarkdownEncoder
    {
        public static string EncodeToMarkdown(this string inputText)
        {
            // Список специальных символов Markdown, которые нужно экранировать
            string[] markdownSpecialChars = { "_", "*", "`", "~", "[", "]", "(", ")", "#", "+", "-", ".", "!" };

            // Эскейпим специальные символы Markdown
            foreach (string specialChar in markdownSpecialChars)
            {
                inputText = Regex.Replace(inputText, Regex.Escape(specialChar), "\\" + specialChar);
            }

            return inputText;
        }
        public static string ConvertMarkdownToHtml(this string markdownText)
        {
            // Создание нового объекта MarkdownPipeline (опционально можно настроить)
            var pipeline = new MarkdownPipelineBuilder().Build();

            // Преобразование текста Markdown в HTML
            string html = Markdown.ToHtml(markdownText, pipeline);

            return html;
        }
        public static string ConvertHtmlToMarkdown(this string html)
        {
            var converter = new Converter();
            string markdown = converter.Convert(html);
            return markdown;
        }
    }
}
