using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using MES.Api.Controllers.Equipment;

namespace MES.Tests.Controllers;

/// <summary>
/// 「设备维修」域授权口径锁定（2026-09-16 拍板）。
///
/// 背景：「设备扫码」（AppMenu 扫码操作组）扫到设备后有三个去向——报修 / 维修 / 工单列表，
/// 现场一线岗位账号只配 Scan 档。原 `api/repair-order` 挂 EquipmentView/Edit/Delete，
/// 现场岗「进得了扫码页却干不了活」（403）。故自 2026-09-16 起**整域放开为仅需登录**：
/// 11 个业务端点一律 `[Authorize]`（不带 Roles），操作人身份由页面内实名选择约束。
///
/// ⚠️ 同时守住「别顺手放宽」：设备台账 `GET api/equipment/all` 仍属「设备管理」页职责，
/// 保持 EquipmentView；维修工单建单页的设备下拉走新增的 `GET api/equipment/options`
/// （仅登录 + 精简 DTO），两处别混。
/// </summary>
public class EquipmentRepairAuthorizationTests
{
    private static MethodInfo Method(Type controller, string name)
        => controller.GetMethod(name)
           ?? throw new InvalidOperationException($"{controller.Name}.{name} 不存在（测试需同步改名）");

    /// <summary>方法级 [Authorize] 是否携带 Roles（= 挂了业务角色档）</summary>
    private static bool RequiresRole(MethodInfo method)
        => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                 .Any(a => !string.IsNullOrEmpty(a.Roles));

    [Theory]
    [InlineData(nameof(RepairOrderController.GetPaged))]
    [InlineData(nameof(RepairOrderController.GetById))]
    [InlineData(nameof(RepairOrderController.Create))]
    [InlineData(nameof(RepairOrderController.CreateBatch))]
    [InlineData(nameof(RepairOrderController.Update))]
    [InlineData(nameof(RepairOrderController.Delete))]
    [InlineData(nameof(RepairOrderController.GetFilterContexts))]
    [InlineData(nameof(RepairOrderController.PrintBatchFile))]
    [InlineData(nameof(RepairOrderController.GetPendingByEquipment))]
    [InlineData(nameof(RepairOrderController.StartRepair))]
    [InlineData(nameof(RepairOrderController.CompleteRepair))]
    public void 维修工单端点_仅需登录_不得带设备角色档(string methodName)
    {
        var method = Method(typeof(RepairOrderController), methodName);

        RequiresRole(method).Should().BeFalse(
            $"RepairOrderController.{methodName} 供「设备扫码」链路的报修/维修页使用，"
            + "挂 EquipmentView/Edit/Delete 会让只配 Scan 档的现场岗 403");

        // 放宽 ≠ 匿名：仍须显式声明方法级 [Authorize]（类级虽已有，方法级显式更耐读、防类级被误删）
        method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Should().NotBeEmpty(
            "放宽为「仅需登录」仍必须保留 [Authorize]，不得变成匿名可调");
    }

    [Fact]
    public void 设备台账all_仍保持EquipmentView档_不得被顺手放宽()
    {
        RequiresRole(Method(typeof(EquipmentController), nameof(EquipmentController.GetAll)))
            .Should().BeTrue(
                "本次只放开「设备维修」域；设备台账全字段列表仍属「设备管理」页职责，"
                + "需选设备的页面请改用 GET api/equipment/options（仅登录）");
    }

    [Fact]
    public void 设备下拉选项_仅需登录_供维修建单页使用()
    {
        var method = Method(typeof(EquipmentController), nameof(EquipmentController.GetOptions));

        RequiresRole(method).Should().BeFalse(
            "维修工单建单页属「设备维修」域（仅需登录），其设备下拉不能依赖 EquipmentView");

        method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Should().NotBeEmpty(
            "放宽为「仅需登录」仍必须保留 [Authorize]");
    }
}
