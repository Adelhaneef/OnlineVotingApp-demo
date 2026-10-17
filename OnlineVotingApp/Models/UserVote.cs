using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineVotingApp.Models
{
    public class UserVote
    {
        public int Id { get; set; }

        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public IdentityUser User { get; set; }

        public int PollId { get; set; }
        public Poll Poll { get; set; }

        public int AnswerOptionId { get; set; }
        public AnswerOption AnswerOption { get; set; }
    }
}