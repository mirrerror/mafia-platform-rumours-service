using MafiaRumoursService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaRumoursService.Data;

public class RumoursDbContext(DbContextOptions<RumoursDbContext> options) : DbContext(options)
{
    public DbSet<Rumour> Rumours { get; set; }
}