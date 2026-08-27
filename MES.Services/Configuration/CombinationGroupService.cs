using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;

namespace MES.Services.Configuration;

/// <summary>
/// 组合归类服务 — 以(工序组, 工段, 产类)为基准的唯一归属映射 CRUD
/// </summary>
public class CombinationGroupService : ICombinationGroupService
{
    private readonly AppDbContext _context;

    public CombinationGroupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CombinationGroupDto>> GetListAsync()
    {
        var rows = await _context.CombinationGroups
            .AsNoTracking()
            .Include(c => c.FlowCategory)
            .OrderBy(c => c.ProcessGroupName)
            .ThenBy(c => c.SectionName)
            .ToListAsync();

        return rows.Select(c => new CombinationGroupDto
        {
            Id = c.Id,
            ProcessGroupName = c.ProcessGroupName,
            SectionName = c.SectionName,
            ProductStatus = c.ProductStatus,
            FlowCategoryId = c.FlowCategoryId,
            ParagraphName = c.ParagraphName,
        }).ToList();
    }

    public async Task<bool> SaveAsync(CombinationGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProcessGroupName) || string.IsNullOrWhiteSpace(dto.SectionName))
            throw new BusinessException("工序组和工段不能为空");
        if (string.IsNullOrWhiteSpace(dto.ProductStatus))
            throw new BusinessException("产类不能为空");
        if (await _context.CombinationGroups.AnyAsync(c => c.Id != dto.Id
                && c.ProcessGroupName == dto.ProcessGroupName
                && c.SectionName == dto.SectionName
                && c.ProductStatus == dto.ProductStatus))
            throw new BusinessException($"组合 \"{dto.ProcessGroupName} / {dto.SectionName} / {dto.ProductStatus}\" 已存在");
        if (!dto.FlowCategoryId.HasValue)
            throw new BusinessException("归属流转类别不能为空");
        if (!await _context.SectionFlowCategorySettings.AnyAsync(s => s.Id == dto.FlowCategoryId.Value))
            throw new BusinessException("归属流转类别不存在");
        if (!string.IsNullOrEmpty(dto.ParagraphName)
            && !await _context.SectionParagraphConfigs.AnyAsync(p => p.ParagraphName == dto.ParagraphName))
            throw new BusinessException($"归属段落 \"{dto.ParagraphName}\" 不存在");

        if (dto.Id > 0)
        {
            var entity = await _context.CombinationGroups.FirstOrDefaultAsync(c => c.Id == dto.Id);
            if (entity == null) return false;

            entity.ProcessGroupName = dto.ProcessGroupName;
            entity.SectionName = dto.SectionName;
            entity.ProductStatus = dto.ProductStatus;
            entity.FlowCategoryId = dto.FlowCategoryId;
            entity.ParagraphName = dto.ParagraphName;
        }
        else
        {
            _context.CombinationGroups.Add(new CombinationGroup
            {
                ProcessGroupName = dto.ProcessGroupName,
                SectionName = dto.SectionName,
                ProductStatus = dto.ProductStatus,
                FlowCategoryId = dto.FlowCategoryId,
                ParagraphName = dto.ParagraphName,
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.CombinationGroups.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return false;

        _context.CombinationGroups.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
