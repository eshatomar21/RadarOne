using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Role Name is required")]
        public string Name { get; set; } = string.Empty;

        // Self-referencing property to establish who reports to whom
        public int? ParentRoleId { get; set; }

        [ForeignKey("ParentRoleId")]
        public virtual Role? ParentRole { get; set; }

        public virtual ICollection<Role> SubordinateRoles { get; set; } = new List<Role>();

        // Navigation property for Users (This ensures when you create the User module, they can link here)
        // public virtual ICollection<User> Users { get; set; } = new List<User>();

        public bool ShareDataWithPeers { get; set; }
        public string? Description { get; set; }
    }

    // ViewModel used specifically to render the tree UI
    public class RoleHierarchyNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<RoleHierarchyNode> Children { get; set; } = new List<RoleHierarchyNode>();
    }
}