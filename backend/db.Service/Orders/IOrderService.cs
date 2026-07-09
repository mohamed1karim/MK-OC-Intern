// Describes what the order-related business logic can do. Same
// dependency-injection pattern as IUserService/IProductService.
using db.Service.DTOs.Orders;

namespace db.Service.Orders;

public interface IOrderService
{
    // createdByUserId is optional — pass null to list every order, or a
    // user's id to list only the orders that user created (used by the
    // frontend's "my orders" view). involvingUserId is separate: it matches
    // orders where the given user was either the creator OR the processor
    // (used by the Admin/SuperAdmin "view this person's order history" page,
    // since an Admin's history includes orders they accepted/completed/
    // cancelled, not just ones they created themselves).
    Task<List<OrderResponseDto>> GetAllAsync(int? createdByUserId, int? involvingUserId = null);
    Task<OrderResponseDto> GetByIdAsync(int id);
    Task<OrderResponseDto> CreateAsync(CreateOrderDto dto);

    // Admin-only status transitions, in order: Confirm (Pending ->
    // Confirmed), then Complete or Cancel (Confirmed -> Completed/Cancelled).
    // Complete/Cancel are no longer reachable directly from Pending.
    Task<OrderResponseDto> ConfirmAsync(int id, ProcessOrderDto dto);
    Task<OrderResponseDto> CompleteAsync(int id, ProcessOrderDto dto);
    Task<OrderResponseDto> CancelAsync(int id, ProcessOrderDto dto);

    // Not in the spec — added for storage/cleanup purposes. If the order is
    // still Pending or Confirmed (not yet finalized), its stock effect gets
    // reversed first (same as Cancel) since the order never actually
    // completed. If it's already Completed/Cancelled, its stock effect is
    // already final/reversed, so deleting it just removes the record.
    Task DeleteAsync(int id);
}
