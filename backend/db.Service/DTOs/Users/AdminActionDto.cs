// Shape of data sent to POST /api/users/{id}/promote and .../demote. Same
// placeholder pattern as ProcessOrderDto (Orders' Confirm/Complete/Cancel):
// since there's no real login/session yet, the acting user's id and role
// are passed manually with the request and checked in the service layer.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Users;

public class AdminActionDto
{
    [Required]
    public int ActingUserId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}
