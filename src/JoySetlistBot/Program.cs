using Serilog;
using SetlistNet;
using SetlistNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;
using SetlistNet.Models.ArrayResult;
using SetlistNet.Models.Enum;
using System.Threading;
using Telegram.Bot.Polling;

namespace JoySetlistBot;

static class Program
{
    private static string botErrorResponse = "Ouch! Your command caused a malfunction, bzz-bzzt. Oops, I better return to start position before something bad happens to me...";
    private static string botSearchNoResultsResponse = "Sorry, I don't know any artist with such or similar name. It seems I can't help you here 😔 but you can try something else.";
    private static string botSearchAdvNoResultsResponse = "Sorry, I don't know any artist with such or similar name. Or perhaps, he hadn't ever played a show at this city or in this year. Guess I can't help you then 😔";
    private static string botSearchResponse = "Please, enter a band name or a part of it to find the setlist:";

    private static string botSearchAdvResponse = "Please, enter the query to find setlists. Use one of the following formats:\r\n" +
                                                 "- <code>artist city</code>\r\n" +
                                                 "- <code>artist year</code>\r\n" +
                                                 "- <code>artist year city</code>\r\n";

    private static string botHelpResponse = "You can search for setlists with following commands:\r\n" +
                                            "/search - Use this command to search for setlists of certain artist.\r\n" +
                                            "/search_adv - This command works the same but you can also specify year and city of setlists. Please, enter the search query in one of the following formats:\r\n" +
                                            "- <code>artist city</code>\r\n" +
                                            "- <code>artist year</code>\r\n" +
                                            "- <code>artist year city</code>\r\n";

    private static Dictionary<long, UserSearchQuery> userQuery = new();
    private static Dictionary<long, Artists> userSearchArtists = new();
    private static Dictionary<long, Setlists> userSearchSetlists = new();
    
    private static Dictionary<long, string> userArtistMbid = new();
    
    
    private static Dictionary<long, string> sessionRequest = new();
    private static Dictionary<long, Location> userLocations = new();

    private static IConfigurationRoot _configuration;
    private static TelegramBotClient _bot;
    private static SetlistApi _setlistFm;
    private static readonly ILogger Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

    static async Task Main()
    {
        ConfigureServices();

        using var cts = new CancellationTokenSource();

        ReceiverOptions receiverOptions = new() { AllowedUpdates = new[]
        {
            UpdateType.Message,
            UpdateType.CallbackQuery
        }};
        _bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);
        
        var me = await _bot.GetMe(cts.Token);
        Console.WriteLine($"Start listening for @{me.Username}");
        Console.ReadLine();

        // Send cancellation request to stop bot
        await cts.CancelAsync();
    }

    private static void ConfigureServices()
    {
        if (_configuration == null)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();
        }

        _setlistFm = new(_configuration["SETLISTFM_API_KEY"]!);
        _bot = new(_configuration["JOYSETLIST_BOT_TOKEN"]!);
    }
        
    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception ex, CancellationToken cancellationToken)
    {
        Logger.Error(ex, "Ошибка перехвачена в HandleErrorAsync");
            
        return Task.CompletedTask;
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        await BotOnUpdate(update);
    }

    private static async Task BotOnUpdate(Update update)
    {
        Logger.Information("Received [{type}].", update.Type);

        switch (update.Type)
        {
            case UpdateType.Message:
                await ProcessMessageUpdate(update.Message);
                break;
            case UpdateType.CallbackQuery:
                await ProcessCallbackQueryUpdate(update.CallbackQuery);
                break;
        }
    }

    private static async Task ProcessMessageUpdate(Message message)
    {
        try
        {
            switch (message.Type)
            {
                case MessageType.Text:
                    await ProcessTextMsg(message);
                    break;
                case MessageType.Location:
                    ProcessLocation(message);
                    break;
                default:
                    await _bot.SendMessage(message.From.Id, "I can't work with " + message.Type + " :(");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Произошла ошибка при обработке сообщения пользователя");
            await _bot.ForwardMessage(80721641, message.Chat.Id, message.MessageId);
            await _bot.SendMessage(80721641, $"**Exception**: {ex.GetType().Name}\r\n**Message**: {ex.Message}\r\n**StackTrace**:\r\n{ex.StackTrace}", ParseMode.Markdown);
        }
    }

    private static async Task ProcessCallbackQueryUpdate(CallbackQuery callbackQuery)
    {
        Logger.Information("Received CallbackQuery. Data: {data}.", callbackQuery.Data);

        var callbackData = callbackQuery.Data.Split(' ');
        if (callbackData[0] == "Edit")
        {
            await ProcessEditSetlist(Convert.ToInt32(callbackData[1]), (int)callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
        }
    }

    // setlistIndex - index of next/previous setlist to load
    private static async Task ProcessEditSetlist(int setlistIndex, long chatId, int messageId)
    {
        var itemsPerPage = userSearchSetlists[chatId] == null ? "null" : userSearchSetlists[chatId].ItemsPerPage.ToString();
        Logger.Information("Process edit setlist. SetlistIndex: {setlistIndex}. ItemsPerPage: {itemsPerPage}.", setlistIndex, itemsPerPage);

        try
        {
            if (setlistIndex == userSearchSetlists[chatId].ItemsPerPage)
            {
                // there is no "Next" button if Page is the last Page, so we can skip the verification
                userSearchSetlists[chatId] = await _setlistFm.SearchSetlists(
                    artistName: userQuery[chatId].ArtistName, 
                    year: userQuery[chatId].EventYear, 
                    cityName: userQuery[chatId].CityName,
                    page: userSearchSetlists[chatId].Page + 1);
                setlistIndex = 0;
            }
            else if (setlistIndex == -1)
            {
                userSearchSetlists[chatId] = await _setlistFm.SearchSetlists(
                    artistName: userQuery[chatId].ArtistName, 
                    year: userQuery[chatId].EventYear, 
                    cityName: userQuery[chatId].CityName,
                    page: userSearchSetlists[chatId].Page - 1);
                setlistIndex = userSearchSetlists[chatId].ItemsPerPage - 1; // because Count of array (i.e. ItemsPerPage) not equals last index in array.
            }

            Setlist setlist = userSearchSetlists[chatId].Setlist[setlistIndex];
            await _bot.EditMessageText(chatId, messageId, Util.SetlistToText(setlist), ParseMode.Html,
                replyMarkup: BotHelpers.GetNextPrevButtons(setlistIndex, userSearchSetlists[chatId], userSearchSetlists[chatId].Setlist[setlistIndex].Url));
        }
        catch (Exception ex)
        {
            Logger.Error("Error occured while ProcessEditSetlist. Exception: {0}. Message: {1}. Inner: {2}.",
                ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
        }
    }

    private static void ProcessLocation(Message message)
    {
        userLocations[message.From.Id] = message.Location;
    }

    private static async Task ProcessTextMsg(Message message)
    {
        Logger.Information("Received Text Msg: {text}. Session of {id}: {2}", message.Text, BotHelpers.GetDisplayName(message.From), sessionRequest.ContainsKey(message.From.Id));

        if (message.Text[0] == '/')
        {
            var commandWords = message.Text.Split(' ');
            string additionParams = null;
            if (commandWords.Length > 0)
                additionParams = string.Join(" ", commandWords, 1, commandWords.Length - 1);

            switch (commandWords[0])
            {
                case "/start":
                    await ProcessStartCmd(message.From.Id);
                    break;
                case "/search":
                    await ProcessSearchCmd(message.From.Id, additionParams);
                    break;
                case "/search_adv":
                    await ProcessSearchAdvCmd(message.From.Id, additionParams);
                    break;
                case "/help":
                    await ProcessHelp(message.From.Id);
                    break;
                default:
                    await _bot.SendMessage(message.From.Id, "I don't know this command :(");
                    break;
            }
        }
        else
        {
            if (sessionRequest.ContainsKey(message.From.Id))
            {
                switch (sessionRequest[message.From.Id])
                {
                    case "bandName":
                        await FindBandsByName(message.Text, message.From.Id);
                        break;
                    case "searchQuery":
                        await FindBandsByQuery(message.Text, message.From.Id);
                        break;
                    case "clarifyBandName":
                        await ClarifyBandName(message.Text, message.From.Id);
                        break;
                    case "recentSetlists":
                        await GetSetlist(message.Text, message.From.Id);
                        break;
                    default:
                        await _bot.SendMessage(message.From.Id, "I don't know what you want, so I searched for some bands.");
                        await FindBandsByName(message.Text, message.From.Id);
                        break;
                }
            }
            else
            {
                await _bot.SendMessage(message.From.Id, "I don't know what you want, so I searched for some bands.");
                await FindBandsByName(message.Text, message.From.Id);
            }
        }
    }

    private static async Task ProcessStartCmd(long chatId)
    {
        await _bot.SendMessage(chatId, "Hi! I'm JoySetlist Bot. I can help you to find setlists.\r\n" +
                                                "I am working with the help of Setlist.FM so I usually show the same setlists you can find there.\r\n" +
                                                "So, why don't you try me? Use /search command and send me the name of your favorite band!");
    }

    private static async Task ProcessSearchCmd(long chatId, string? artistName = null)
    {
        if (string.IsNullOrEmpty(artistName))
        {
            await _bot.SendMessage(chatId, botSearchResponse, replyMarkup: BotHelpers.ForceReply());
            sessionRequest[chatId] = "bandName";
        }
        else
        {
            await FindBandsByName(artistName, chatId);
        }
    }

    private static async Task ProcessSearchAdvCmd(long chatId, string searchQuery = null)
    {
        if (string.IsNullOrEmpty(searchQuery))
        {
            await _bot.SendMessage(chatId, botSearchAdvResponse, replyMarkup: BotHelpers.ForceReply(), parseMode: ParseMode.Html);
            sessionRequest[chatId] = "searchQuery";
        }
        else
        {
            await FindBandsByQuery(searchQuery, chatId);
        }
    }

    private static async Task FindBandsByName(string bandName, long chatId)
    {
        try
        {
            var artists = await _setlistFm.SearchArtists(artistName: bandName, sort: ArtistSort.Relevance);
            if (artists.Artist.Count == 1)
            {
                await FindRecentSetlists(artists.Artist[0].MBID, chatId);
            }
            else
            {
                userSearchArtists[chatId] = artists;
                await _bot.SendMessage(chatId, "Please, select a band you would like to see setlists for:",
                    replyMarkup: BotHelpers.OptionsKeyboard(artists.Artist.Select(a => a.GetNameWithDisambiguation()).ToArray()));
                sessionRequest[chatId] = "clarifyBandName";
            }
        }
        catch (WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await _bot.SendMessage(chatId, botSearchNoResultsResponse, replyMarkup: BotHelpers.HideKeyboard());
                sessionRequest.Remove(chatId);
            }
            else
            {
                Logger.Error("WebException occured while FindBandsByName. Exception: {0}. Message: {1}. Inner: {2}.",
                    ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error occured while FindBandsByName. Exception: {0}. Message: {1}. Inner: {2}.",
                ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
            await _bot.SendMessage(chatId, botErrorResponse, replyMarkup: BotHelpers.HideKeyboard());
            sessionRequest.Remove(chatId);
        }
    }

    private static async Task FindBandsByQuery(string searchQuery, long chatId)
    {
        try
        {
            var searchFields = Util.ParseQuery(searchQuery);
            var setlists = await _setlistFm.SearchSetlists(
                artistName: searchFields.ArtistName,
                year: searchFields.EventYear,
                cityName: searchFields.CityName
                );

            userQuery[chatId] = searchFields;
            userSearchSetlists[chatId] = setlists;
            await _bot.SendMessage(chatId, Util.SetlistsToTextHtml(setlists), ParseMode.Html, replyMarkup:
                BotHelpers.OptionsKeyboard(setlists.Setlist.Select(s => s.EventDate.ToString("dd.MM.yyyy"))));

            sessionRequest[chatId] = "recentSetlists";
        }
        catch (WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await _bot.SendMessage(chatId, botSearchAdvNoResultsResponse, replyMarkup: BotHelpers.HideKeyboard());
                sessionRequest.Remove(chatId);
            }
            else
            {
                Logger.Error("WebException occured while FindBandsByQuery. Exception: {0}. Message: {1}. Inner: {2}.",
                    ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error occured while FindBandsByQuery. Exception: {0}. Message: {1}. Inner: {2}.",
                ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
            await _bot.SendMessage(chatId, botErrorResponse, replyMarkup: BotHelpers.HideKeyboard());
            sessionRequest.Remove(chatId);
        }
    }

    private static async Task ClarifyBandName(string nameWithDisambiguation, long chatId)
    {
        try
        {
            var artist = userSearchArtists[chatId].Artist.FirstOrDefault(a => a.GetNameWithDisambiguation().Equals(nameWithDisambiguation));
            if (artist != null)
            {
                await FindRecentSetlists(artist.MBID, chatId);
            }
            else
            {
                await _bot.SendMessage(chatId,
                    $"Sorry, but I don't know any artist called \"{nameWithDisambiguation}\".");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error occured while ClarifyBandName. Exception: {0}. Message: {1}. Inner: {2}.",
                ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
        }
    }

    private static async Task FindRecentSetlists(string mbid, long chatId)
    {
        try
        {
            var artistSetlists = await _setlistFm.ArtistSetlists(mbid);

            userArtistMbid[chatId] = mbid;
            userSearchSetlists[chatId] = artistSetlists;
            await _bot.SendMessage(chatId, Util.SetlistsToTextHtml(artistSetlists), ParseMode.Html, replyMarkup:
                BotHelpers.OptionsKeyboard(artistSetlists.Setlist.Select(s => s.EventDate.ToString("dd.MM.yyyy"))));

            sessionRequest[chatId] = "recentSetlists";
        }
        catch (Exception ex)
        {
            Logger.Error("Error occured while FindRecentSetlists. Exception: {0}. Message: {1}. Inner: {2}.",
                ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
        }
    }

    private static async Task GetSetlist(string eventDate, long chatId)
    {
        try
        {
            var setlist = userSearchSetlists[chatId].Setlist.FirstOrDefault(s => s.EventDate.ToString("dd.MM.yyyy").Equals(eventDate, StringComparison.InvariantCulture));

            int setlistIndex = 0;
            foreach (var s in userSearchSetlists[chatId].Setlist)
            {
                if (s == setlist)
                {
                    break;
                }

                setlistIndex++;
            }

            if (setlistIndex != -1)
            {
                await _bot.SendMessage(chatId, $"You can navigate through \"{userSearchSetlists[chatId].Setlist[setlistIndex].Artist.Name}\" setlists via Previous and Next buttons.", replyMarkup: BotHelpers.HideKeyboard());
                var text = Util.SetlistToText(userSearchSetlists[chatId].Setlist[setlistIndex]);
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup:
                    BotHelpers.GetNextPrevButtons(setlistIndex, userSearchSetlists[chatId], userSearchSetlists[chatId].Setlist[setlistIndex].Url));
            }
            else
            {
                await _bot.SendMessage(chatId, $"There aren't any setlists from {eventDate}.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error occured while GetSetlist. Exception: {0}. Message: {1}. Inner: {2}.",
                ex.GetType().Name, ex.Message, ex.InnerException == null ? "null" : ex.InnerException.GetType().Name + ", " + ex.InnerException.Message);
        }
    }

    private static async Task ProcessHelp(long chatId)
    {
        await _bot.SendMessage(chatId, botHelpResponse, ParseMode.Html);
    }
}