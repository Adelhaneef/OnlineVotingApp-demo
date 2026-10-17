namespace OnlineVotingApp.Models
{
    public class PollWithUserVote
    {
        public Poll Poll { get; set; }
        public int? SelectedOptionId { get; set; }
    }
}
