using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

const string archiveUrl = "https://the-eye.eu/ReckfulArchive/";
string streamDate = args[0];

using HttpClient httpClient = new();

_ = httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("eye02");

using HttpResponseMessage response = await httpClient.GetAsync(archiveUrl);

if (!response.IsSuccessStatusCode) return;

using Stream stream = await response.Content.ReadAsStreamAsync();
using IBrowsingContext browsingContext = BrowsingContext.New();
using IDocument document = await browsingContext.OpenAsync(m => m.Content(stream));

IList<IElement> streamElements = document
    .QuerySelectorAll("body > div.ui.center.aligned.grid > div > div.ui.left.aligned.stacked.segment > a")
    .GroupBy(element => element.TextContent.Split(" ")[0], element => element)
    .FirstOrDefault(group => group.Key == streamDate)
    .ToList();

List<Vod> vods = new List<Vod>();

DateTime? timestamp = null;
string videoUrl = string.Empty;
string thumbnailUrl = string.Empty;
string infoUrl = string.Empty;

foreach (IElement element in streamElements)
{
    if (element.TextContent.EndsWith(".info.json"))
    {
        if (!string.IsNullOrWhiteSpace(infoUrl))
        {
            vods.Add(new(timestamp, videoUrl, thumbnailUrl, infoUrl));
        }

        infoUrl = archiveUrl + element.Attributes["href"].Value;
        timestamp = await GetTimestamp(infoUrl);
    }
    else if (element.TextContent.EndsWith(".jpg"))
    {
        thumbnailUrl = archiveUrl + element.Attributes["href"].Value;
    }
    else if (element.TextContent.EndsWith(".mp4"))
    {
        videoUrl = archiveUrl + element.Attributes["href"].Value;
    }
}

Console.Out.WriteLine(JsonSerializer.Serialize(vods.OrderBy(vod => vod.Timestamp), new() { WriteIndented = true }));

async Task<DateTime?> GetTimestamp(string infoUrl)
{
    using HttpResponseMessage response = await httpClient.GetAsync(infoUrl);

    response.EnsureSuccessStatusCode();

    using JsonDocument json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    long timestamp = json.RootElement.GetProperty("timestamp").GetInt64();

    return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
}

record Vod(DateTime? Timestamp, string VideoUrl, string ThumbnailUrl, string InfoUrl);