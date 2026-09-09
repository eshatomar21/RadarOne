using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;

// Ensure this matches your project name
using Radar_CRM.Models;

namespace Radar_CRM.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<Account> Accounts { get; set; }

        public DbSet<Lead> Leads { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<Notes> Note { get; set; }

        public DbSet<Deal> Deals { get; set; }

        public DbSet<Product> Products { get; set; }

        public DbSet<Vendor> Vendors { get; set; }

        public DbSet<DealNote> DealNotes { get; set; }

        public DbSet<ProductPaymentRow> ProductPaymentRows{ get; set; }

        public DbSet<DealPaymentRow> DealPaymentRows { get; set; }
        public DbSet<Radar_CRM.Models.Task> Tasks { get; set; }




        // This creates a table named 'Customers' based on your Customer model

    }
}
