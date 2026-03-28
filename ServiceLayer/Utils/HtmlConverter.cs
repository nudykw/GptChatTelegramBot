using HtmlAgilityPack;

namespace ServiceLayer.Utils
{
    public static class HtmlTagFilter
    {
        private static string[] telegramTags = new string[]
            {
                "b", "strong",
                "i", "em",
                "code",
                "s", "strike", "del",
                "u",
                "pre"
            };
        public static string ConvertHtmlToTelegramHtml(this string html)
        {
            var telegramHtml = KeepOnlySelectedTags(html, telegramTags);
            return telegramHtml;
        }
        private static string KeepOnlySelectedTags(string html, params string[] selectedTags)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            // Получаем все теги
            HtmlNodeCollection allTags = htmlDocument.DocumentNode.SelectNodes("//*");

            if (allTags != null)
            {
                foreach (HtmlNode? tag in allTags)
                {
                    if (!selectedTags.Contains(tag.Name.ToLower()))
                    {
                        tag.Name = string.Empty;
                        //tag.ParentNode.ReplaceChild(tag.FirstChild, tag);
                        //Remove();
                    }
                }
            }

            var rawHtml = htmlDocument.DocumentNode.OuterHtml;
            rawHtml = rawHtml.Replace("<>", string.Empty).Replace("</>", string.Empty);
            return rawHtml;
        }
    }
}
