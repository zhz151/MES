// 文件路径: MES.Services/GradeMappingService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 牌号对照服务实现
/// </summary>
public class GradeMappingService : IGradeMappingService
{
    private readonly AppDbContext _context;

    public GradeMappingService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取所有牌号对照（用于下拉框）
    /// </summary>
    public async Task<List<StandardGradeMappingDto>> GetAllAsync()
    {
        var items = await _context.StandardGradeMappings
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.StandardGrade)
            .Select(g => new StandardGradeMappingDto
            {
                Id = g.Id,
                StandardGrade = g.StandardGrade,
                PlantGrade = g.PlantGrade,
                Density = g.Density,
                HeatTreatment = g.HeatTreatment,
                SpecialMaterial = g.SpecialMaterial,  // 直接赋值 bool，不转换
                SpecialNote = g.SpecialNote,
                Remark = g.Remark
            })
            .ToListAsync();

        return items;
    }

    /// <summary>
    /// 根据ID获取牌号对照详情
    /// </summary>
    public async Task<StandardGradeMappingDto> GetByIdAsync(int id)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("牌号对照不存在");
        }

        return new StandardGradeMappingDto
        {
            Id = entity.Id,
            StandardGrade = entity.StandardGrade,
            PlantGrade = entity.PlantGrade,
            Density = entity.Density,
            HeatTreatment = entity.HeatTreatment,
            SpecialMaterial = entity.SpecialMaterial,  // 直接赋值 bool，不转换
            SpecialNote = entity.SpecialNote,
            Remark = entity.Remark
        };
    }

    /// <summary>
    /// 创建牌号对照
    /// </summary>
    public async Task<StandardGradeMappingDto> CreateAsync(CreateGradeMappingRequest request)
    {
        // 检查标准牌号唯一性
        var exists = await _context.StandardGradeMappings
            .AnyAsync(g => g.StandardGrade == request.StandardGrade && !g.IsDeleted);

        if (exists)
        {
            throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");
        }

        var entity = new StandardGradeMapping
        {
            StandardGrade = request.StandardGrade,
            PlantGrade = request.PlantGrade,
            Density = request.Density,
            HeatTreatment = request.HeatTreatment,
            SpecialMaterial = request.SpecialMaterial,  // 直接赋值 bool
            SpecialNote = request.SpecialNote,
            Remark = request.Remark
        };

        _context.StandardGradeMappings.Add(entity);
        await _context.SaveChangesAsync();

        return new StandardGradeMappingDto
        {
            Id = entity.Id,
            StandardGrade = entity.StandardGrade,
            PlantGrade = entity.PlantGrade,
            Density = entity.Density,
            HeatTreatment = entity.HeatTreatment,
            SpecialMaterial = entity.SpecialMaterial,
            SpecialNote = entity.SpecialNote,
            Remark = entity.Remark
        };
    }

    /// <summary>
    /// 更新牌号对照
    /// </summary>
    public async Task<StandardGradeMappingDto> UpdateAsync(int id, UpdateGradeMappingRequest request)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("牌号对照不存在");
        }

        // 检查标准牌号唯一性（排除自身）
        if (!string.IsNullOrEmpty(request.StandardGrade) && request.StandardGrade != entity.StandardGrade)
        {
            var exists = await _context.StandardGradeMappings
                .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.Id != id && !g.IsDeleted);

            if (exists)
            {
                throw new BusinessException($"标准牌号 '{request.StandardGrade}' 已存在");
            }
            entity.StandardGrade = request.StandardGrade;
        }

        if (!string.IsNullOrEmpty(request.PlantGrade))
        {
            entity.PlantGrade = request.PlantGrade;
        }

        if (request.Density.HasValue)
        {
            entity.Density = request.Density.Value;
        }

        if (request.HeatTreatment != null)
        {
            entity.HeatTreatment = request.HeatTreatment;
        }

        if (request.SpecialMaterial.HasValue)
        {
            entity.SpecialMaterial = request.SpecialMaterial.Value;
        }

        if (request.SpecialNote != null)
        {
            entity.SpecialNote = request.SpecialNote;
        }

        if (request.Remark != null)
        {
            entity.Remark = request.Remark;
        }

        await _context.SaveChangesAsync();

        return new StandardGradeMappingDto
        {
            Id = entity.Id,
            StandardGrade = entity.StandardGrade,
            PlantGrade = entity.PlantGrade,
            Density = entity.Density,
            HeatTreatment = entity.HeatTreatment,
            SpecialMaterial = entity.SpecialMaterial,
            SpecialNote = entity.SpecialNote,
            Remark = entity.Remark
        };
    }

    /// <summary>
    /// 删除牌号对照（软删除）
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("牌号对照不存在");
        }

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}