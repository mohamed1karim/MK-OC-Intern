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
    //
    // callerId/callerRole come from the caller's validated JWT. When the
    // caller isn't Admin/SuperAdmin, the implementation forces
    // createdByUserId to the caller's own id and drops involvingUserId,
    // regardless of what was requested — a plain User can only ever see
    // their own orders. Admin/SuperAdmin callers keep the full filtering
    // freedom above.
    Task<List<OrderResponseDto>> GetAllAsync(int? createdByUserId, int? involvingUserId, int callerId, string callerRole);

    // Same ownership rule as GetAllAsync: a plain User may only view an
    // order they created or processed; Admin/SuperAdmin may view any order.
    Task<OrderResponseDto> GetByIdAsync(int id, int callerId, string callerRole);

    // createdByUserId comes from the caller's validated JWT — the creator is
    // always whoever is making the request.
    Task<OrderResponseDto> CreateAsync(CreateOrderDto dto, int createdByUserId);

    // Admin-only status transitions, in order: Confirm (Pending ->
    // Confirmed), then Complete or Cancel (Confirmed -> Completed/Cancelled).
    // Complete/Cancel are no longer reachable directly from Pending.
    // actingUserId/actingRole come from the caller's validated JWT.
    Task<OrderResponseDto> ConfirmAsync(int id, int actingUserId, string actingRole);
    Task<OrderResponseDto> CompleteAsync(int id, int actingUserId, string actingRole);
    Task<OrderResponseDto> CancelAsync(int id, int actingUserId, string actingRole);

    // Admin/SuperAdmin only. Not in the spec — added for storage/cleanup
    // purposes. If the order is still Pending or Confirmed (not yet
    // finalized), its stock effect gets reversed first (same as Cancel)
    // since the order never actually completed. If it's already Completed/
    // Cancelled, its stock effect is already final/reversed, so deleting it
    // just removes the record.
    Task DeleteAsync(int id, string actingRole);
}
