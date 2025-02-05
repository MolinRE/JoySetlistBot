using SetlistNet.Models.Abstract;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types.ReplyMarkups;

namespace JoySetlistBot;

public static class BotHelpers
{
    public static InlineKeyboardMarkup GetNextPrevButtons(int index, ApiArrayResult pagesInfo, string url)
    {
        InlineKeyboardMarkup reply;
        if (pagesInfo.Total == 1)
        {
            var keyboard = new List<InlineKeyboardButton>();
            keyboard.Add(new InlineKeyboardButton("Setlist FM") { Url = url });

            reply = new InlineKeyboardMarkup(keyboard);
            return reply;
        }

        var maxIndex = (pagesInfo.Total % pagesInfo.ItemsPerPage) - 1;
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
        var keyboard = args.Select(s => new KeyboardButton[] { s });
        return new ReplyKeyboardMarkup(keyboard)
        {
            OneTimeKeyboard = true
        };
    }
    
    public static IReplyMarkup OptionsKeyboard(IEnumerable<string> buttons)
    {
        var keyboard = buttons.Select(s => new KeyboardButton[] { s });
        return new ReplyKeyboardMarkup(keyboard)
        {
            OneTimeKeyboard = true
        };
    }

    public static IReplyMarkup HideKeyboard() => new ReplyKeyboardRemove();

    public static IReplyMarkup ForceReply() => new ForceReplyMarkup();

    public static string GetDisplayName(Telegram.Bot.Types.User user)
    {
        if (string.IsNullOrEmpty(user.Username))
        {
            var result = user.FirstName;
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