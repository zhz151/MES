using MES.Core.Constants;
using MES.Data.Entities;

namespace MES.Services.Extensions;

/// <summary>
/// ProcessGroup 的扩展方法，用于替代散落在各 Service 中的重复 switch 映射。
/// </summary>
public static class ProcessGroupExtensions
{
    /// <summary>
    /// 根据工段名称从工序组中获取对应的执行序号。
    /// 先匹配标准工段名，再通过别名查找。
    /// </summary>
    public static int? GetSectionSequence(this ProcessGroup pg, string sectionName) => sectionName switch
    {
        SectionDefs.ColdRollDraw => pg.ColdRollDraw,
        SectionDefs.OilPipeCut => pg.OilPipeCut,
        SectionDefs.Degrease => pg.Degrease,
        SectionDefs.Solution => pg.Solution,
        SectionDefs.Straighten => pg.Straighten,
        SectionDefs.Cut => pg.Cut,
        SectionDefs.ThicknessMeasure => pg.ThicknessMeasure,
        SectionDefs.Pickle => pg.Pickle,
        SectionDefs.OuterPolish => pg.OuterPolish,
        SectionDefs.InnerGrinding => pg.InnerGrinding,
        SectionDefs.OuterSpotGrinding => pg.OuterSpotGrinding,
        SectionDefs.Inspection => pg.Inspection,
        SectionDefs.WeldingHead => pg.WeldingHead,
        SectionDefs.Lubrication => pg.Lubrication,
        SectionDefs.Warehouse => pg.Warehouse,
        // 别名匹配
        _ when SectionDefs.Aliases.TryGetValue(sectionName, out var standard) =>
            pg.GetSectionSequence(standard),
        _ => null
    };

    /// <summary>
    /// 获取工序组中所有非空工段（含序号），按执行顺序排序
    /// </summary>
    public static List<(string SectionName, int SequenceNumber)> GetNonEmptySections(this ProcessGroup pg)
    {
        var result = new List<(string SectionName, int SequenceNumber)>(15);

        void AddIfHasValue(int? value, string name)
        {
            if (value.HasValue)
                result.Add((name, value.Value));
        }

        AddIfHasValue(pg.ColdRollDraw, SectionDefs.ColdRollDraw);
        AddIfHasValue(pg.OilPipeCut, SectionDefs.OilPipeCut);
        AddIfHasValue(pg.Degrease, SectionDefs.Degrease);
        AddIfHasValue(pg.Solution, SectionDefs.Solution);
        AddIfHasValue(pg.Straighten, SectionDefs.Straighten);
        AddIfHasValue(pg.Cut, SectionDefs.Cut);
        AddIfHasValue(pg.ThicknessMeasure, SectionDefs.ThicknessMeasure);
        AddIfHasValue(pg.Pickle, SectionDefs.Pickle);
        AddIfHasValue(pg.OuterPolish, SectionDefs.OuterPolish);
        AddIfHasValue(pg.InnerGrinding, SectionDefs.InnerGrinding);
        AddIfHasValue(pg.OuterSpotGrinding, SectionDefs.OuterSpotGrinding);
        AddIfHasValue(pg.Inspection, SectionDefs.Inspection);
        AddIfHasValue(pg.WeldingHead, SectionDefs.WeldingHead);
        AddIfHasValue(pg.Lubrication, SectionDefs.Lubrication);
        AddIfHasValue(pg.Warehouse, SectionDefs.Warehouse);

        return result.OrderBy(s => s.SequenceNumber).ToList();
    }
}
