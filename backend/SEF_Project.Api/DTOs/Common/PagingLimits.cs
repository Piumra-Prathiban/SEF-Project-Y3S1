namespace SEF_Project.Api.DTOs.Common;

/// <summary>
/// Upper bounds for paged queries. <c>(page - 1) * pageSize</c> is passed to
/// <c>Skip</c> as an <see cref="int"/>: an unbounded page number overflows it,
/// and PostgreSQL rejects the negative OFFSET with a 500.
/// </summary>
public static class PagingLimits
{
    public const int MaxPage = 10_000;

    public const int MaxPageSize = 100;
}
