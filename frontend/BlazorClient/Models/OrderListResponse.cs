using System.Collections.Generic;

namespace BlazorClient.Models;

public record OrderListResponse(
    List<Order> Items,
    PaginationData Pagination
);
