using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OnlineVotingApp.Models
{
    public class Poll
    {
        public int Id { get; set; }

        [Required] 
        public string Question  { get; set; }

        [Required]
        public DateTime fromDate { get; set; }
        
        
        [Required]
        public DateTime toDate { get; set; }

        [Required]
        public int displayOrder {  get; set; }

        public List<AnswerOption> AnswerOptions { get; set; } = new List<AnswerOption>();


    }
}
