// Shape of data sent to DELETE /api/products/{id}. Same placeholder pattern
// as Users' AdminActionDto — since there's no real login/session yet, the
// acting user's id and role are passed manually with the request and
// checked in the service layer.
using System.ComponentModel.DataAnnotations;

namespace db.Service.DTOs.Products;

public class ProductActionDto
{
    [Required]
    public int ActingUserId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}
