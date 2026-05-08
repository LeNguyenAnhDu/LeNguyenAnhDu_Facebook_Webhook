using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FB.Webhook.Shared.Models;

/// <summary>
/// Model để deserialize payload từ Facebook Webhook
/// </summary>
public class FacebookWebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; set; }

    [JsonPropertyName("entry")]
    public List<FacebookEntry> Entry { get; set; } = new();
}

public class FacebookEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("changes")]
    public List<FacebookChange> Changes { get; set; } = new();

    [JsonPropertyName("messaging")]
    public List<FacebookMessaging> Messaging { get; set; } = new();
}

public class FacebookChange
{
    [JsonPropertyName("field")]
    public string Field { get; set; }

    [JsonPropertyName("value")]
    public FacebookValue Value { get; set; }
}

public class FacebookValue
{
    [JsonPropertyName("from")]
    public FacebookFrom From { get; set; }

    [JsonPropertyName("item")]
    public string Item { get; set; }

    [JsonPropertyName("post_id")]
    public string PostId { get; set; }

    [JsonPropertyName("comment_id")]
    public string CommentId { get; set; }

    [JsonPropertyName("verb")]
    public string Verb { get; set; } // add, edit, remove

    [JsonPropertyName("created_time")]
    public long CreatedTime { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class FacebookFrom
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class FacebookMessaging
{
    [JsonPropertyName("sender")]
    public FacebookSender Sender { get; set; }

    [JsonPropertyName("recipient")]
    public FacebookSender Recipient { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("message")]
    public FacebookMessage Message { get; set; }
}

public class FacebookSender
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
}

public class FacebookMessage
{
    [JsonPropertyName("mid")]
    public string Mid { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}

