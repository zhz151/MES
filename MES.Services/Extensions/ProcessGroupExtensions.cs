using MES.Core.Constants;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;

namespace MES.Services.Extensions;

/// <summary>
/// ProcessGroup 的扩展方法，用于替代散落在各 Service 中的重复 switch 映射。
/// </summary>
public static class ProcessGroupExtensions
{
    /// <summary>
    /// 根据工段名（Key/规范中文/别名均可）从工序组中获取对应的执行序号。
    /// 入参先经 SectionKeys.ToKey 归一为稳定 Key 再匹配。
    /// </summary>
    public static int? GetSectionSequence(this ProcessGroup pg, string? sectionName)
    {
        var key = SectionKeys.ToKey(sectionName);
        if (key == null) return null;
        return key switch
        {
            SectionKeys.ColdRollDraw => pg.ColdRollDraw,
            SectionKeys.OilPipeCut => pg.OilPipeCut,
            SectionKeys.Degrease => pg.Degrease,
            SectionKeys.EmulsionWash => pg.EmulsionWash,
            SectionKeys.UltrasonicWash => pg.UltrasonicWash,
            SectionKeys.ClothPolish => pg.ClothPolish,
            SectionKeys.BrightAnnealing => pg.BrightAnnealing,
            SectionKeys.Solution => pg.Solution,
            SectionKeys.Straighten => pg.Straighten,
            SectionKeys.Cut => pg.Cut,
            SectionKeys.ThicknessMeasure => pg.ThicknessMeasure,
            SectionKeys.Pickle => pg.Pickle,
            SectionKeys.OuterPolish => pg.OuterPolish,
            SectionKeys.InnerPolish => pg.InnerPolish,
            SectionKeys.InnerGrinding => pg.InnerGrinding,
            SectionKeys.OuterSpotGrinding => pg.OuterSpotGrinding,
            SectionKeys.SandBlasting => pg.SandBlasting,
            SectionKeys.ShotBlasting => pg.ShotBlasting,
            SectionKeys.Inspection => pg.Inspection,
            SectionKeys.WeldingHead => pg.WeldingHead,
            SectionKeys.Welding => pg.Welding,
            SectionKeys.Lubrication => pg.Lubrication,
            SectionKeys.Packing => pg.Packing,
            SectionKeys.Warehouse => pg.Warehouse,
            SectionKeys.Extra1 => pg.Extra1,
            SectionKeys.Extra2 => pg.Extra2,
            _ => null
        };
    }

    /// <summary>
    /// 获取工序组中所有非空工段（含序号），按执行顺序排序
    /// </summary>
    public static List<(string SectionName, int SequenceNumber)> GetNonEmptySections(this ProcessGroup pg)
    {
        var result = new List<(string SectionName, int SequenceNumber)>(26);

        void AddIfHasValue(int? value, string name)
        {
            if (value.HasValue)
                result.Add((name, value.Value));
        }

        AddIfHasValue(pg.ColdRollDraw, SectionDefs.ColdRollDraw);
        AddIfHasValue(pg.OilPipeCut, SectionDefs.OilPipeCut);
        AddIfHasValue(pg.Degrease, SectionDefs.Degrease);
        AddIfHasValue(pg.EmulsionWash, SectionDefs.EmulsionWash);
        AddIfHasValue(pg.UltrasonicWash, SectionDefs.UltrasonicWash);
        AddIfHasValue(pg.ClothPolish, SectionDefs.ClothPolish);
        AddIfHasValue(pg.BrightAnnealing, SectionDefs.BrightAnnealing);
        AddIfHasValue(pg.Solution, SectionDefs.Solution);
        AddIfHasValue(pg.Straighten, SectionDefs.Straighten);
        AddIfHasValue(pg.Cut, SectionDefs.Cut);
        AddIfHasValue(pg.ThicknessMeasure, SectionDefs.ThicknessMeasure);
        AddIfHasValue(pg.Pickle, SectionDefs.Pickle);
        AddIfHasValue(pg.OuterPolish, SectionDefs.OuterPolish);
        AddIfHasValue(pg.InnerPolish, SectionDefs.InnerPolish);
        AddIfHasValue(pg.InnerGrinding, SectionDefs.InnerGrinding);
        AddIfHasValue(pg.OuterSpotGrinding, SectionDefs.OuterSpotGrinding);
        AddIfHasValue(pg.SandBlasting, SectionDefs.SandBlasting);
        AddIfHasValue(pg.ShotBlasting, SectionDefs.ShotBlasting);
        AddIfHasValue(pg.Inspection, SectionDefs.Inspection);
        AddIfHasValue(pg.WeldingHead, SectionDefs.WeldingHead);
        AddIfHasValue(pg.Welding, SectionDefs.Welding);
        AddIfHasValue(pg.Lubrication, SectionDefs.Lubrication);
        AddIfHasValue(pg.Packing, SectionDefs.Packing);
        AddIfHasValue(pg.Warehouse, SectionDefs.Warehouse);
        AddIfHasValue(pg.Extra1, SectionDefs.Extra1);
        AddIfHasValue(pg.Extra2, SectionDefs.Extra2);

        return result.OrderBy(s => s.SequenceNumber).ToList();
    }

    /// <summary>
    /// 获取工序组中所有非空工段的稳定 Key（含序号），按执行顺序排序。
    /// 供存储值校验/派生写入使用（记录/批次 SectionName 存英文 Key）。
    /// </summary>
    public static List<(string SectionKey, int SequenceNumber)> GetNonEmptySectionKeys(this ProcessGroup pg)
        => pg.GetNonEmptySections()
            .Select(s => (SectionKeys.ToKey(s.SectionName)!, s.SequenceNumber))
            .Where(x => x.Item1 != null)
            .ToList();
}
