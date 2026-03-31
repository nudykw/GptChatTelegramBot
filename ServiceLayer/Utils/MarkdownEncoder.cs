using Markdig;
using ReverseMarkdown;
using System.Text.RegularExpressions;

namespace ServiceLayer.Utils
{
    public static class MarkdownEncoder
    {
        public static string EncodeToMarkdown(this string inputText)
        {
            if (string.IsNullOrEmpty(inputText)) return inputText;

            // List of special characters for MarkdownV2 that must be escaped
            // https://core.telegram.org/bots/api#markdownv2-style
            string[] markdownSpecialChars = { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };

            // Escape special Markdown characters
            foreach (string specialChar in markdownSpecialChars)
            {
                inputText = inputText.Replace(specialChar, "\\" + specialChar);
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

        public static string WrapWithSpoiler(this string text, global::Telegram.Bot.Types.Enums.ParseMode mode, bool encode = true)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            if (mode == global::Telegram.Bot.Types.Enums.ParseMode.Html)
            {
                var escaped = encode ? text.Replace("<", "&lt;").Replace(">", "&gt;") : text;
                return $"<tg-spoiler><i>{escaped}</i></tg-spoiler>";
            }
            else
            {
                var escaped = encode ? text.EncodeToMarkdown() : text;
                return $"||_{escaped}_||";
            }
        }
    }
}
