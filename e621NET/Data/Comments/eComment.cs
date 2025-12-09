using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace e621NET.Data.Comments
{
    public class eComment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("post_id")]
        public int PostId { get; set; }

        [JsonPropertyName("creator_id")]
        public int CreatorId { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("updater_id")]
        public int UpdaterId { get; set; }

        [JsonPropertyName("is_hidden")]
        public bool IsHidden { get; set; }

        [JsonPropertyName("is_sticky")]
        public bool IsSticky { get; set; }

        /// <summary>
        /// Can be "warning", "record", "ban"
        /// </summary>
        [JsonPropertyName("warning_type")]
        public string? WarningType { get; set; }

        [JsonPropertyName("warning_user_id")]
        public int WarningUserId { get; set; }

        [JsonPropertyName("creator_name")]
        public string? CreatorName { get; set; }

        [JsonPropertyName("updater_name")]
        public string? UpdaterName { get; set; }
    }
}
