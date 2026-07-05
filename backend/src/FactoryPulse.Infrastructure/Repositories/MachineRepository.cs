using System;
using System.Collections.Generic;
using System.Text;
using FactoryPulse.Application.Interfaces;
using FactoryPulse.Domain.Entities;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Repositories;

public class MachineRepository : IMachineRepository
{
    private readonly FactoryPulseDbContext _context;

    public MachineRepository(FactoryPulseDbContext context)
    {
        _context = context;
    }

    public async Task<Machine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Machines.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Machine>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Machines.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        await _context.Machines.AddAsync(machine, cancellationToken);
    }

    public void Update(Machine machine)
    {
        _context.Machines.Update(machine);
    }

    public void Remove(Machine machine)
    {
        _context.Machines.Remove(machine);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

}
