using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OnlineVotingApp.Models;
using System.Text.Json.Serialization;

public class AnswerOption
{
    public int Id { get; set; }

    public string OptionText { get; set; }

    public int VoteCount { get; set; }

    public int PollId { get; set; }

    [JsonIgnore]
    [ValidateNever]
    public Poll? Poll { get; set; }
}
