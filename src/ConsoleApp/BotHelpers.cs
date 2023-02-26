using SetlistNet.Models;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types.ReplyMarkups;

namespace JoySetlistBotConsole;

public static class BotHelpers
{
    public static InlineKeyboardMarkup GetNextPrevButtons<T>(int index, ApiArrayResult<T> pagesInfo, string url)
    {
        InlineKeyboardMarkup reply;
        if (pagesInfo.Count == 1)
        {
            var keyboard = new List<InlineKeyboardButton>();
            keyboard.Add(new InlineKeyboardButton("Setlist FM") { Url = url });

            reply = new InlineKeyboardMarkup(keyboard);
            return reply;
        }

        int maxIndex = (pagesInfo.Total % pagesInfo.ItemsPerPage) - 1;
        if (maxIndex == -1)
            maxIndex = pagesInfo.ItemsPerPage;

        if (index == maxIndex && pagesInfo.Page == pagesInfo.TotalPages)
        {
            var keyboard = new InlineKeyboardButton[2];
            keyboard[0] = new InlineKeyboardButton("◀️ Previous") { CallbackData = "Edit " + (index - 1) };
            keyboard[1] = new InlineKeyboardButton("Setlist FM") { Url = url };

            reply = new InlineKeyboardMarkup(keyboard);
        }
        else if (index == 0 && pagesInfo.Page == 1)
        {
            var keyboard = new InlineKeyboardButton[2];
            keyboard[0] = new InlineKeyboardButton("Setlist FM") { Url = url };
            keyboard[1] = new InlineKeyboardButton("Next ▶️") { CallbackData = "Edit " + (index + 1) };

            reply = new InlineKeyboardMarkup(keyboard);
        }
        else
        {
            var keyboard = new InlineKeyboardButton[3];
            keyboard[0] = new InlineKeyboardButton("◀️ Previous") { CallbackData = "Edit " + (index - 1) };
            keyboard[1] = new InlineKeyboardButton("Setlist FM") { Url = url };
            keyboard[2] = new InlineKeyboardButton("Next ▶️") { CallbackData = "Edit " + (index + 1) };

            reply = new InlineKeyboardMarkup(keyboard);
        }

        return reply;
    }

    public static IReplyMarkup OptionsKeyboard(params string[] args)
    {
        var keyboard = args.Select(s => new KeyboardButton[] { s }).ToArray();
        return new ReplyKeyboardMarkup(keyboard);
    }

    public static IReplyMarkup HideKeyboard()
    {
        return new ReplyKeyboardRemove();
    }

    public static IReplyMarkup ForceReply()
    {
        return new ForceReplyMarkup();
    }

    public static string GetDisplayName(Telegram.Bot.Types.User user)
    {
        if (string.IsNullOrEmpty(user.Username))
        {
            string result = user.FirstName;
            if (!string.IsNullOrEmpty(user.LastName))
            {
                result += " " + user.LastName;
            }

            return string.IsNullOrEmpty(result) ? user.Id.ToString() : result;
        }
        else
        {
            return user.Username;
        }
    }
}