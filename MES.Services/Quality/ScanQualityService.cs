using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Quality;

namespace MES.Services.Quality;

/// <summary>
/// 扫码链质量服务 — 「巡检」「不合格反馈」的扫码入口包装。
///
/// 只做三件事：按登录账号解析实名、固化数据来源为 SCAN、转发到质量模块既有服务，
/// 避免扫码链污染质量模块的业务逻辑与角色档（质量服务仍受 QualityView/Edit 保护）。
/// </summary>
public class ScanQualityService : IScanQualityService
{
    /// <summary>扫码报工数据来源标记（与质量模块 MANUAL 区分）</summary>
    private const string ScanDataSource = "SCAN";

    private readonly IInspectionPatrolService _patrolService;
    private readonly INonconformingFeedbackService _feedbackService;
    private readonly IEmployeeService _employeeService;
    private readonly ICurrentUser _currentUser;

    public ScanQualityService(
        IInspectionPatrolService patrolService,
        INonconformingFeedbackService feedbackService,
        IEmployeeService employeeService,
        ICurrentUser currentUser)
    {
        _patrolService = patrolService;
        _feedbackService = feedbackService;
        _employeeService = employeeService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 登录名 → 员工真实姓名。扫码员工账号的用户名即员工工号（见 EmployeeService.SyncAccountsAsync），
    /// 查不到档案（如管理员代录）时回退登录名，保证「巡检人」永不为空。
    /// </summary>
    public async Task<string> GetCurrentOperatorNameAsync()
    {
        var loginName = _currentUser.GetUserName();
        var employee = await _employeeService.GetByCodeAsync(loginName);
        return string.IsNullOrWhiteSpace(employee?.Name) ? loginName : employee!.Name;
    }

    // ========== 巡检 ==========

    public Task<List<InspectionPatrolDto>> GetPatrolsByKeyAsync(string batchNo, int processGroupId, string sectionName)
        => _patrolService.GetByKeyAsync(batchNo, processGroupId, sectionName);

    public Task<InspectionPatrolDto?> GetPatrolAsync(int id) => _patrolService.GetByIdAsync(id);

    public async Task<InspectionPatrolDto> CreatePatrolAsync(CreateInspectionPatrolRequest request)
    {
        request.Inspector = await GetCurrentOperatorNameAsync();
        request.DataSource = ScanDataSource;
        return await _patrolService.CreateAsync(request);
    }

    public async Task<InspectionPatrolDto> RectifyPatrolAsync(int id, InspectionPatrolRectifyRequest request)
    {
        request.RectificationOperator = await GetCurrentOperatorNameAsync();
        return await _patrolService.RectifyAsync(id, request);
    }

    public Task<InspectionPatrolAttachmentDto> AddPatrolAttachmentAsync(
        int patrolId, string photoType, Stream content, string fileName, string contentType)
        => _patrolService.AddAttachmentAsync(patrolId, photoType, content, fileName, contentType);

    public Task<AttachmentContent?> GetPatrolAttachmentContentAsync(int patrolId, int attachmentId)
        => _patrolService.GetAttachmentContentAsync(patrolId, attachmentId);

    public Task DeletePatrolAttachmentAsync(int patrolId, int attachmentId)
        => _patrolService.DeleteAttachmentAsync(patrolId, attachmentId);

    public Task<InspectionPatrolLookupResultDto?> LookupPatrolBatchAsync(string batchNo)
        => _patrolService.LookupBatchAsync(batchNo);

    public Task<InspectionPatrolPositionOptionsDto> GetPatrolPositionOptionsAsync()
        => _patrolService.GetPositionOptionsAsync();

    // ========== 不合格反馈 ==========

    public async Task<NonconformingFeedbackDto> CreateFeedbackAsync(CreateNonconformingFeedbackRequest request)
    {
        request.Reporter = await GetCurrentOperatorNameAsync();
        request.DataSource = ScanDataSource;
        return await _feedbackService.CreateAsync(request);
    }

    public Task<NonconformingFeedbackAttachmentDto> AddFeedbackAttachmentAsync(
        int feedbackId, Stream content, string fileName, string contentType)
        => _feedbackService.AddAttachmentAsync(feedbackId, content, fileName, contentType);

    public Task<AttachmentContent?> GetFeedbackAttachmentContentAsync(int feedbackId, int attachmentId)
        => _feedbackService.GetAttachmentContentAsync(feedbackId, attachmentId);

    public Task DeleteFeedbackAttachmentAsync(int feedbackId, int attachmentId)
        => _feedbackService.DeleteAttachmentAsync(feedbackId, attachmentId);

    public Task<NonconformingFeedbackLookupResultDto?> LookupFeedbackBatchAsync(string batchNo)
        => _feedbackService.LookupBatchAsync(batchNo);
}
