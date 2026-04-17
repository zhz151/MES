// 文件路径: MES.Services/GradeMappingService.cs
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// Grade mapping service implementation
/// </summary>
public class GradeMappingService : IGradeMappingService
{
    private readonly AppDbContext _context;

    public GradeMappingService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all grade mappings (for dropdown)
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
                SpecialMaterial = g.SpecialMaterial,  // Direct assignment bool, no conversion
                SpecialNote = g.SpecialNote,
                Remark = g.Remark
            })
            .ToListAsync();

        return items;
    }

    /// <summary>
    /// Get grade mapping details by ID
    /// </summary>
    public async Task<StandardGradeMappingDto> GetByIdAsync(int id)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Grade mapping does not exist");
        }

        return new StandardGradeMappingDto
        {
            Id = entity.Id,
            StandardGrade = entity.StandardGrade,
            PlantGrade = entity.PlantGrade,
            Density = entity.Density,
            HeatTreatment = entity.HeatTreatment,
            SpecialMaterial = entity.SpecialMaterial,  // Direct assignment bool, no conversion
            SpecialNote = entity.SpecialNote,
            Remark = entity.Remark
        };
    }

    /// <summary>
    /// Create grade mapping
    /// </summary>
    public async Task<StandardGradeMappingDto> CreateAsync(CreateGradeMappingRequest request)
    {
        // Check standard grade uniqueness
        var exists = await _context.StandardGradeMappings
            .AnyAsync(g => g.StandardGrade == request.StandardGrade && !g.IsDeleted);

        if (exists)
        {
            throw new BusinessException($"Standard grade '{request.StandardGrade}' already exists");
        }

        var entity = new StandardGradeMapping
        {
            StandardGrade = request.StandardGrade,
            PlantGrade = request.PlantGrade,
            Density = request.Density,
            HeatTreatment = request.HeatTreatment,
            SpecialMaterial = request.SpecialMaterial,  // Direct assignment bool
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
    /// Update grade mapping
    /// </summary>
    public async Task<StandardGradeMappingDto> UpdateAsync(int id, UpdateGradeMappingRequest request)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Grade mapping does not exist");
        }

        // Check standard grade uniqueness (exclude self)
        if (!string.IsNullOrEmpty(request.StandardGrade) && request.StandardGrade != entity.StandardGrade)
        {
            var exists = await _context.StandardGradeMappings
                .AnyAsync(g => g.StandardGrade == request.StandardGrade && g.Id != id && !g.IsDeleted);

            if (exists)
            {
                throw new BusinessException($"Standard grade '{request.StandardGrade}' already exists");
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
    /// Delete grade mapping (soft delete)
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.StandardGradeMappings
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (entity == null)
        {
            throw new BusinessException("Grade mapping does not exist");
        }

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }
}