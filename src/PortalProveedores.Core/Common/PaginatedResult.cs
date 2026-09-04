namespace PortalProveedores.Core.Common;

/// <summary>
/// Contenedor fuertemente tipado para resultados paginados de consultas.
/// </summary>
/// <typeparam name="T">Tipo de los elementos contenidos en la página.</typeparam>
public class PaginatedResult<T>
{
    public IReadOnlyCollection<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalRecords { get; }
    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PaginatedResult(IReadOnlyCollection<T> items, int totalRecords, int pageNumber, int pageSize)
    {
        Items = items ?? Array.Empty<T>();
        TotalRecords = totalRecords < 0 ? 0 : totalRecords;
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = pageSize < 1 ? 10 : pageSize;
    }
}
