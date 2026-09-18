using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Radar_CRM.Services
{
    public interface IHierarchyService
    {
        Task<List<string>> GetAccessibleUserIdsAsync(string currentUserId);
    }

    public class HierarchyService : IHierarchyService
    {
        private readonly ApplicationDbContext _context;

        public HierarchyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetAccessibleUserIdsAsync(string currentUserId)
        {
            var accessibleUserIds = new List<string> { currentUserId };

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || currentUser.RoleId == null)
            {
                // If they have no role, they can only see themselves
                return accessibleUserIds;
            }

            // Admin Override: Admins get access to every user's ID
            if (currentUser.Role == "Administrator" || currentUser.Role == "Admin")
            {
                return await _context.Users.Select(u => u.Id).ToListAsync();
            }

            // Standard Hierarchy Logic: Get roles below the current user
            var allRoles = await _context.Roles.ToListAsync();
            var subordinateRoleIds = GetSubordinateRoleIds(allRoles, currentUser.RoleId);

            if (subordinateRoleIds.Any())
            {
                var subordinateUserIds = await _context.Users
                    .Where(u => u.RoleId.HasValue && subordinateRoleIds.Contains(u.RoleId.Value))
                    .Select(u => u.Id)
                    .ToListAsync();

                accessibleUserIds.AddRange(subordinateUserIds);
            }

            return accessibleUserIds.Distinct().ToList();
        }

        private List<int> GetSubordinateRoleIds(List<Role> allRoles, int? currentRoleId)
        {
            var subordinateIds = new List<int>();
            if (currentRoleId == null) return subordinateIds;

            // Find immediate children
            var directChildren = allRoles.Where(r => r.ParentRoleId == currentRoleId).Select(r => r.Id).ToList();
            subordinateIds.AddRange(directChildren);

            // Recursively find children of children
            foreach (var childId in directChildren)
            {
                subordinateIds.AddRange(GetSubordinateRoleIds(allRoles, childId));
            }

            return subordinateIds;
        }
    }
}