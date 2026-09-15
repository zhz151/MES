using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using MES.Api.Controllers.Batch;
using MES.Api.Controllers.Order;

namespace MES.Tests.Controllers;

/// <summary>
/// 首页查询卡数据源端点的授权口径锁定（2026-09-14 拍板）。
///
/// 背景：首页 <c>Index.razor</c> 只有 <c>[Authorize]</c>（仅登录），而「订单进度查询」「生产批次进度查询」
/// 两张卡要求**所有登录用户都能查**。因此三个端点自 2026-09-14 起放宽为仅登录、不带业务角色档：
///   · <c>GET api/order/progress</c>（原 OrderView）
///   · <c>GET api/batch/by-batch-no/{batchNo}</c>（原 BatchView）
///   · <c>GET api/batch/{id}/tracking</c>（原 BatchView）
/// 本测试是这条决策的护栏：谁把它们改回角色档，首页卡就会对无对应权限的用户 403（静默失效）。
///
/// ⚠️ 同时守住「别顺手放宽」：按 Id 取批次详情 <c>GET api/batch/{id}</c> 仍属「生产执行」页职责，保持 BatchView。
/// </summary>
public class HomeQueryEndpointAuthorizationTests
{
    private static MethodInfo Method(Type controller, string name)
        => controller.GetMethod(name)
           ?? throw new InvalidOperationException($"{controller.Name}.{name} 不存在（测试需同步改名）");

    /// <summary>方法级 [Authorize] 是否携带 Roles（= 挂了业务角色档）</summary>
    private static bool RequiresRole(MethodInfo method)
        => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                 .Any(a => !string.IsNullOrEmpty(a.Roles));

    [Theory]
    [InlineData(typeof(OrderProgressController), nameof(OrderProgressController.GetProgress))]
    [InlineData(typeof(BatchController), nameof(BatchController.GetByBatchNo))]
    [InlineData(typeof(BatchController), nameof(BatchController.GetTrackingVisual))]
    public void 首页查询三端点_仅需登录_不得带业务角色档(Type controller, string methodName)
    {
        var method = Method(controller, methodName);

        RequiresRole(method).Should().BeFalse(
            $"{controller.Name}.{methodName} 供首页查询卡使用，而首页只有 [Authorize]；"
            + "挂 OrderView/BatchView 会让无对应权限的用户一查就 403");

        // 放宽 ≠ 匿名：仍须显式声明方法级 [Authorize]（类级虽已有，方法级显式更耐读、防类级被误删）
        method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Should().NotBeEmpty(
            "放宽为「仅需登录」仍必须保留 [Authorize]，不得变成匿名可读");
    }

    [Fact]
    public void 按Id取批次详情_仍保持BatchView档_不得被顺手放宽()
    {
        RequiresRole(Method(typeof(BatchController), nameof(BatchController.GetById))).Should().BeTrue(
            "本次只放开「按生产编号取批次」与「执行进度」两个首页入口；按 Id 取详情仍是「生产执行」页职责");
    }
}
