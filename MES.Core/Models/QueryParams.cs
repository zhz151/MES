using System.ComponentModel.DataAnnotations;

namespace MES.Core.Models;


public class QueryParams
{

    [Range(1, int.MaxValue, ErrorMessage = "椤电爜蹇呴』澶т簬0")]
    public int PageIndex { get; set; } = 1;


    [Range(1, 100, ErrorMessage = "姣忛〉鏉℃暟蹇呴』鍦?-100涔嬮棿")]
    public int PageSize { get; set; } = 20;


    public string? Keyword { get; set; }


    public string SortBy { get; set; } = "CreatedTime";


    public bool IsDescending { get; set; } = true;


    public int Skip => (PageIndex - 1) * PageSize;
}