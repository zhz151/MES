using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Infrastructure;
using MES.Core.Interfaces.Infrastructure;
using MES.Data;
using MES.Data.Entities.Infrastructure;

namespace MES.Services.Infrastructure;

public class OperationLogService : IOperationLogService
{
    private readonly AppDbContext _context;

    public OperationLogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddLogAsync(string module, int entityId, string operationType, string? detail = null)
    {
        var log = new OperationLog
        {
            Module = module,
            EntityId = entityId,
            OperationType = operationType,
            Detail = detail
        };
        _context.OperationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<OperationLogDto>> GetLogsAsync(string module, int entityId)
    {
        return await _context.OperationLogs
            .Where(l => l.Module == module && l.EntityId == entityId)
            .OrderByDescending(l => l.CreatedTime)
            .Select(l => new OperationLogDto
            {
                Id = l.Id,
                Module = l.Module,
                EntityId = l.EntityId,
                OperationType = l.OperationType,
                Detail = l.Detail,
                CreatedBy = l.CreatedBy,
                CreatedTime = l.CreatedTime
            })
            .ToListAsync();
    }
}
