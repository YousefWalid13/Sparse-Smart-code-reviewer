using System.Text.Json.Serialization;

namespace Sparse_Smart_code_reviewer.External.Github
{
    public class GitHubEmailResponse
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }
}
