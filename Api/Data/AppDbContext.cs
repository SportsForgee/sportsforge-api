using Microsoft.EntityFrameworkCore;
using Api.Models;
 
namespace Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
 
        public DbSet<User> Users { get; set; }
    }
}
