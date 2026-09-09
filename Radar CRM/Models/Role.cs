using System.ComponentModel.DataAnnotations;

namespace Radar_CRM.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }

        public string RoleName {  get; set; }


    }
}
