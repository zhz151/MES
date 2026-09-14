using FluentAssertions;
using MES.Core.Models;

namespace MES.Tests;

/// <summary>
/// ApiResponse 重载语义锁定测试。
///
/// ⚠️ 背景（2026-09-14 事故）：ApiResponse&lt;T&gt; 曾同时存在
/// <c>Ok(T data, string message = "操作成功")</c> 与 <c>Ok(string message = "操作成功")</c>。
/// 当 T=string 时单参调用按 C# 决议规则（所有参数都有实参者优于需补默认参数者）绑定到 message-only 版本，
/// 于是 ApiResponse&lt;string&gt;.Ok("张三") 把值写进 Message、Data 留 null 且编译期无提示 ——
/// 扫码链「当前巡检人」端点因此返回 data=null，前端取不到实名，提交被杀成「巡检人不能为空/反馈人不能为空」。
/// message-only 重载已删除，以下用例锁死「单参 Ok 必须写 Data」。
/// </summary>
public class ApiResponseTests
{
    [Fact]
    public void Ok_泛型为string且单参_值写入Data()
    {
        var response = ApiResponse<string>.Ok("张三");

        response.Success.Should().BeTrue();
        response.Data.Should().Be("张三");
        response.Message.Should().Be("操作成功");
    }

    [Fact]
    public void Ok_泛型为object且单参传string_值写入Data()
    {
        var response = ApiResponse<object>.Ok("张三");

        response.Data.Should().Be("张三");
        response.Message.Should().Be("操作成功");
    }

    [Fact]
    public void Ok_两参_自定义消息写入Message()
    {
        var response = ApiResponse<string>.Ok("张三", "查询成功");

        response.Data.Should().Be("张三");
        response.Message.Should().Be("查询成功");
    }
}
