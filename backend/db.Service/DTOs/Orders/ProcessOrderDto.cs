// Shape of data sent to POST /api/orders/{id}/complete and .../cancel.
// Since there's no login yet, the spec requires the acting user's id and
// role to be passed manually with the request instead of read from a
// session — this is that placeholder, explicitly not real security (the
// spec calls this out directly; it becomes enforced for real once the
// Auth bonus adds actual login).
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Orders;

public class ProcessOrderDto
{
    [Required]
    public int ActingUserId { get; set; }

    // Sent as the enum's name: "User" or "Admin". Only "Admin" is allowed to
    // complete/cancel an order — OrderService checks this and throws
    // ForbiddenException otherwise.
    [Required]
    public string Role { get; set; } = string.Empty;
}
