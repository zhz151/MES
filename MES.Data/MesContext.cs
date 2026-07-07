using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace MES.Data;

public partial class MesContext : DbContext
{
    public MesContext()
    {
    }

    public MesContext(DbContextOptions<MesContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Server=.;Database=MES;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
