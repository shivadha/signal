using System.Text.Json.Serialization;

namespace Signal.Application.Common.Models;

public record TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; init; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; init; }
}

public record TelegramMessage
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; init; }

    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; init; } = null!;

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("date")]
    public long Date { get; init; }
}

public record TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "private";
}

public record TelegramCallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }
}

public record TelegramInlineKeyboardButton
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    [JsonPropertyName("callback_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CallbackData { get; init; }
}

public record TelegramInlineKeyboardMarkup
{
    [JsonPropertyName("inline_keyboard")]
    public List<List<TelegramInlineKeyboardButton>> InlineKeyboard { get; init; } = new();
}

public record TelegramSendMessagePayload
{
    [JsonPropertyName("chat_id")]
    public string ChatId { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("parse_mode")]
    public string ParseMode { get; init; } = "HTML";

    [JsonPropertyName("reply_markup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TelegramInlineKeyboardMarkup? ReplyMarkup { get; init; }
}

