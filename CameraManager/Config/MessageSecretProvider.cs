using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CameraManager;

public static class MessageSecretProvider
{
    private const string ConfigDirectoryName = "Config Setting";
    private const string SecretsFileName = "MessageSecrets.ini";

    private const string TelegramPlaceholder = "YOUR_TELEGRAM_BOT_TOKEN";
    private const string DiscordTokenPlaceholder = "YOUR_DISCORD_BOT_TOKEN";
    private const string DiscordChannelPlaceholder = "YOUR_DISCORD_CHANNEL_ID";
    private const string ZaloApiKeyPlaceholder = "YOUR_ZALO_ESMS_API_KEY";
    private const string ZaloSecretKeyPlaceholder = "YOUR_ZALO_ESMS_SECRET_KEY";
    private const string ZaloOaidPlaceholder = "YOUR_ZALO_OAID";
    private const string ZaloTemplateIdPlaceholder = "YOUR_ZALO_TEMPLATE_ID";
    private const string ZaloBrandNamePlaceholder = "YOUR_ZALO_BRAND_NAME";

    private static string BaseDirectory
    {
        get
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                return baseDir;
            }

            // Fall back to current directory only when AppContext does not provide a value
            return Directory.GetCurrentDirectory();
        }
    }

    public static string SecretsDirectory => Path.Combine(BaseDirectory, ConfigDirectoryName);
    public static string SecretsFilePath => Path.Combine(SecretsDirectory, SecretsFileName);

    public static MessageSecrets GetSecrets()
    {
        EnsureSecretsFileExists();

        var data = ParseIniFile(SecretsFilePath);

        var (telegramToken, telegramPlaceholder) = ResolveValue(data, "Telegram.BotToken", TelegramPlaceholder);
        var (discordToken, discordTokenPlaceholder) = ResolveValue(data, "Discord.BotToken", DiscordTokenPlaceholder);
        var (discordChannelRaw, discordChannelPlaceholder) = ResolveValue(data, "Discord.ChannelId", DiscordChannelPlaceholder);
        var (zaloApiKey, zaloApiKeyPlaceholder) = ResolveValue(data, "Zalo.ApiKey", ZaloApiKeyPlaceholder);
        var (zaloSecretKey, zaloSecretKeyPlaceholder) = ResolveValue(data, "Zalo.SecretKey", ZaloSecretKeyPlaceholder);
        var (zaloOaid, zaloOaidPlaceholder) = ResolveValue(data, "Zalo.OAID", ZaloOaidPlaceholder);
        var (zaloTemplateId, zaloTemplateIdPlaceholder) = ResolveValue(data, "Zalo.TemplateId", ZaloTemplateIdPlaceholder);
        var (zaloBrandName, zaloBrandNamePlaceholder) = ResolveValue(data, "Zalo.BrandName", ZaloBrandNamePlaceholder);

        data.TryGetValue("Zalo.CallbackUrl", out var zaloCallbackUrlRaw);
        data.TryGetValue("Zalo.CampaignId", out var zaloCampaignIdRaw);

        var zaloCallbackUrl = zaloCallbackUrlRaw?.Trim() ?? string.Empty;
        var zaloCampaignId = zaloCampaignIdRaw?.Trim() ?? string.Empty;

        ulong? discordChannelId = null;
        if (!discordChannelPlaceholder && !string.IsNullOrWhiteSpace(discordChannelRaw))
        {
            if (ulong.TryParse(discordChannelRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                discordChannelId = parsed;
            }
        }

        return new MessageSecrets
        {
            TelegramBotToken = telegramToken,
            TelegramBotTokenIsPlaceholder = telegramPlaceholder,
            DiscordBotToken = discordToken,
            DiscordBotTokenIsPlaceholder = discordTokenPlaceholder,
            DiscordChannelId = discordChannelId,
            DiscordChannelIdIsPlaceholder = discordChannelPlaceholder || (!discordChannelId.HasValue && string.IsNullOrWhiteSpace(discordChannelRaw)),
            EsmsApiKey = zaloApiKey,
            EsmsApiKeyIsPlaceholder = zaloApiKeyPlaceholder,
            EsmsSecretKey = zaloSecretKey,
            EsmsSecretKeyIsPlaceholder = zaloSecretKeyPlaceholder,
            EsmsOaid = zaloOaid,
            EsmsOaidIsPlaceholder = zaloOaidPlaceholder,
            EsmsTemplateId = zaloTemplateId,
            EsmsTemplateIdIsPlaceholder = zaloTemplateIdPlaceholder,
            EsmsBrandName = zaloBrandName,
            EsmsBrandNameIsPlaceholder = zaloBrandNamePlaceholder,
            EsmsCallbackUrl = zaloCallbackUrl,
            EsmsCampaignId = zaloCampaignId
        };
    }

    private static void EnsureSecretsFileExists()
    {
        if (!Directory.Exists(SecretsDirectory))
        {
            Directory.CreateDirectory(SecretsDirectory);
        }

        if (!File.Exists(SecretsFilePath))
        {
            var template = BuildTemplate();
            File.WriteAllText(SecretsFilePath, template, Encoding.UTF8);
        }
    }

    private static Dictionary<string, string> ParseIniFile(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string currentSection = string.Empty;

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
            {
                continue;
            }

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (!string.IsNullOrEmpty(currentSection))
            {
                key = $"{currentSection}.{key}";
            }

            result[key] = value;
        }

        return result;
    }

    private static (string Value, bool IsPlaceholder) ResolveValue(Dictionary<string, string> data, string key, string placeholder)
    {
        if (!data.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return (string.Empty, true);
        }

        value = value.Trim();
        var isPlaceholder = value.Equals(placeholder, StringComparison.OrdinalIgnoreCase);
        return (value, isPlaceholder);
    }

    private static string BuildTemplate()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Message delivery secrets");
        builder.AppendLine("# Replace the placeholder values with the real credentials in your deployment environment.");
        builder.AppendLine("# This file is generated automatically and stays outside source control.");
        builder.AppendLine();
        builder.AppendLine("[Telegram]");
        builder.AppendLine($"BotToken={TelegramPlaceholder}");
        builder.AppendLine();
        builder.AppendLine("[Discord]");
        builder.AppendLine($"BotToken={DiscordTokenPlaceholder}");
        builder.AppendLine($"ChannelId={DiscordChannelPlaceholder}");
        builder.AppendLine();
        builder.AppendLine("[Zalo]");
        builder.AppendLine($"ApiKey={ZaloApiKeyPlaceholder}");
        builder.AppendLine($"SecretKey={ZaloSecretKeyPlaceholder}");
        builder.AppendLine($"OAID={ZaloOaidPlaceholder}");
        builder.AppendLine($"TemplateId={ZaloTemplateIdPlaceholder}");
        builder.AppendLine($"BrandName={ZaloBrandNamePlaceholder}");
        builder.AppendLine("CallbackUrl=");
        builder.AppendLine("CampaignId=");
        return builder.ToString();
    }
}

public sealed class MessageSecrets
{
    public string TelegramBotToken { get; init; } = string.Empty;
    public bool TelegramBotTokenIsPlaceholder { get; init; }
    public string DiscordBotToken { get; init; } = string.Empty;
    public bool DiscordBotTokenIsPlaceholder { get; init; }
    public ulong? DiscordChannelId { get; init; }
    public bool DiscordChannelIdIsPlaceholder { get; init; }
    public string EsmsApiKey { get; init; } = string.Empty;
    public bool EsmsApiKeyIsPlaceholder { get; init; }
    public string EsmsSecretKey { get; init; } = string.Empty;
    public bool EsmsSecretKeyIsPlaceholder { get; init; }
    public string EsmsOaid { get; init; } = string.Empty;
    public bool EsmsOaidIsPlaceholder { get; init; }
    public string EsmsTemplateId { get; init; } = string.Empty;
    public bool EsmsTemplateIdIsPlaceholder { get; init; }
    public string EsmsBrandName { get; init; } = string.Empty;
    public bool EsmsBrandNameIsPlaceholder { get; init; }
    public string EsmsCallbackUrl { get; init; } = string.Empty;
    public string EsmsCampaignId { get; init; } = string.Empty;

    public bool HasTelegramConfiguration => !TelegramBotTokenIsPlaceholder && !string.IsNullOrWhiteSpace(TelegramBotToken);

    public bool HasDiscordConfiguration =>
        !DiscordBotTokenIsPlaceholder &&
        !string.IsNullOrWhiteSpace(DiscordBotToken) &&
        DiscordChannelId.HasValue &&
        !DiscordChannelIdIsPlaceholder;

    public bool HasZaloCredentials =>
        !EsmsApiKeyIsPlaceholder &&
        !string.IsNullOrWhiteSpace(EsmsApiKey) &&
        !EsmsSecretKeyIsPlaceholder &&
        !string.IsNullOrWhiteSpace(EsmsSecretKey);

    public bool HasZaloTemplateConfiguration =>
        !EsmsOaidIsPlaceholder &&
        !string.IsNullOrWhiteSpace(EsmsOaid) &&
        !EsmsTemplateIdIsPlaceholder &&
        !string.IsNullOrWhiteSpace(EsmsTemplateId) &&
        !EsmsBrandNameIsPlaceholder &&
        !string.IsNullOrWhiteSpace(EsmsBrandName);
}
