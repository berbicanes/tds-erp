using Microsoft.EntityFrameworkCore;

namespace Tds.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
