using OnlineVotingApp.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
public class AnswerOption
{
    public int Id { get; set; }

    public string OptionText { get; set; }

    public int VoteCount { get; set; }

    public int PollId { get; set; }
    
    [JsonIgnore]              // ✅ Prevent model binding
    [NotMapped] // ✅ Do NOT bind Poll during form post
    public Poll? Poll { get; set; }
}
