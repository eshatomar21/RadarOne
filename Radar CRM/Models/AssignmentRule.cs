using System;
using System.ComponentModel.DataAnnotations;

namespace Radar_CRM.Models
{
    public class AssignmentRule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Rule Name")]
        public string RuleName { get; set; }

        [Required]
        [Display(Name = "Apply to Module")]
        public string TargetModule { get; set; } // e.g., "Accounts", "Leads"

        // Condition Details
        [Required]
        [Display(Name = "Field to Check")]
        public string ConditionField { get; set; } // e.g., "DataSource"

        [Required]
        [Display(Name = "Operator")]
        public string ConditionOperator { get; set; } // e.g., "Equals", "Contains"

        [Required]
        [Display(Name = "Match Value")]
        public string ConditionValue { get; set; } // e.g., "Meta Ads"

        // Assignment Action
        [Required]
        [Display(Name = "Assign Record To")]
        public string AssignToUserId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}