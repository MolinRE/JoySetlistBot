using System;
using System.Globalization;
using System.Linq;
using System.Text;
using SetlistNet.Models;
using SetlistNet.Models.ArrayResult;

namespace JoySetlistBot;

public static class Util
{
    /// <summary>
    /// Converts given setlist to text view. Use this method to get text representation of the setlist.
    /// </summary>
    /// <param name="setlist">The setlist to represent.</param>
    /// <param name="useHtml">Whether or not to use HTML-code to add little extra beauty.</param>
    /// <returns>Text representation of the setlist</returns>
    public static string SetlistToText(Setlist setlist, bool useHtml = true)
    {
        var text = new StringBuilder();
        var venue = setlist.Venue;
        text.AppendLine($"[{setlist.EventDate.ToString("MMM dd yyyy", CultureInfo.GetCultureInfo("en-US"))}] {TagHelper.Href(setlist.Artist.UrlStats, setlist.Artist.Name)} setlist");
        text.AppendLine($"at {TagHelper.Href(venue.Url, $"{venue.Name}, {venue.City.Name}, {venue.City.Country.Name}")}");
        if (!string.IsNullOrEmpty(setlist.Tour?.Name))
        {
            text.AppendLine($"Tour: {setlist.Tour.Name}");
        }

        var count = 0;
        foreach (var set in setlist.Sets.Set)
        {
            if (set.Encore.HasValue || !string.IsNullOrEmpty(set.Name))
            {
                text.AppendLine();
                var setName = set.Name;
                if (set.Encore.HasValue)
                    setName = "Encore " + set.Encore;
                if (setName != " ")
                {
                    if (useHtml)
                        text.AppendLine("<b>" + setName + "</b>");
                    else
                        text.AppendLine(setName);
                }
            }

            foreach (var song in set.Songs)
            {
                // "ONE OK ROCK" - "The Pilot </3"
                var songName = song.Name
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("&", "&amp;");
                
                text.AppendFormat("{0}. {1}", ++count, songName.Trim() == "" ? "Unknown" : songName);
                if (song.Cover != null || song.With != null)
                {
                    text.Append(" (");
                    if (song.Cover != null)
                    {
                        text.AppendFormat("<i>{0}</i> cover", song.Cover.Name);
                        if (song.With != null)
                            text.AppendFormat(" w/ {0}", song.With.Name);
                    }
                    else if (song.With != null)
                        text.AppendFormat("w/ {0}", song.With.Name);

                    text.Append(")");
                }

                if (!string.IsNullOrEmpty(song.Info))
                {
                    text.AppendFormat(" ({0})", song.Info);
                }

                text.AppendLine();
            }
        }

        if (!string.IsNullOrEmpty(setlist.Info))
        {
            text.AppendLine();
            text.AppendLine($"<b>Note</b>: {setlist.Info}");
        }

        return text.ToString();
    }

    public static string SetlistsToText(Setlists setlists, int count = 7, bool useHtml = true)
    {
        var text = new StringBuilder();
        foreach (var setlist in setlists.Setlist.Take(count))
        {
            text.AppendFormat("[{0:dd.MM.yyyy}, {2}] {1}, {3}.",
                setlist.EventDate, setlist.Venue.City.Name, setlist.Venue.City.Country.Code, setlist.Venue.Name);
            if (!string.IsNullOrEmpty(setlist.Tour?.Name))
            {
                text.AppendFormat(" ({0} tour)", setlist.Tour.Name);
            }

            if (!string.IsNullOrEmpty(setlist.Info))
            {
                text.AppendFormat(". Note: {0}", setlist.Info);
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    public static string SetlistsToTextHtml(Setlists setlists, int count = 7, bool useHtml = true)
    {
        var text = new StringBuilder();
        var year = DateTime.Now.Year;
        foreach (var setlist in setlists.Setlist.Take(count))
        {
            var date = setlist.EventDate;
            if (date.Year != year)
            {
                year = date.Year;
                text.AppendLine($"<b>{year}</b>:");
            }

            text.AppendFormat("[{0:dd.MM}] {1} {2}.",
                setlist.EventDate, setlist.Venue.City.Name, setlist.Venue.City.Country.Code, setlist.Venue.Name);
            text.AppendLine();
        }

        return text.ToString();
    }

    // Parse the query string and return Setlist object used to search for setlists
    public static UserSearchQuery? ParseQuery(string query)
    {
        if (query.Length == 0)
        {
            return null;
        }

        var result = new UserSearchQuery();
        string[] keywords = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (IsYear(keywords[^1]))
        {
            result.EventYear = int.Parse(keywords[^1]);
            result.ArtistName = string.Join(" ", keywords.Take(keywords.Length - 1));
        }
        else
        {
            result.CityName = keywords[^1];
            if (keywords.Length > 2 && IsYear(keywords[^2]))
            {
                result.EventYear = int.Parse(keywords[^2]);
                result.ArtistName = string.Join(" ", keywords.Take(keywords.Length - 2));
            }
            else
            {
                result.ArtistName = string.Join(" ", keywords.Take(keywords.Length - 1));
            }
        }

        return result;
    }

    // Simple check whether given string contains 4 digits
    private static bool IsYear(string p)
    {
        return p.Length == 4 && p.All(char.IsDigit);
    }
}