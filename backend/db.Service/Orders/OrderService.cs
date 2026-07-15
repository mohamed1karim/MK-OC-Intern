// This is where the project's real business rules live: stock changes,
// snapshotting prices, role-gated status transitions, and reversible
// cancellation. Same overall shape as UserService/ProductService (talk to
// AppDbcontext, map to/from DTOs, throw exceptions for the middleware to
// turn into HTTP responses) — just with more steps per method, because
// Orders actually has business logic to enforce.
using db.Context;
using db.Context.Model;
using db.Service.DTOs.Orders;
using db.Service.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace db.Service.Orders;

public class OrderService : IOrderService
{
    private readonly AppDbcontext _context;

    public OrderService(AppDbcontext context)
    {
        _context = context;
    }

    // Both reads need each order's items, each item's product (so the
    // response can include the product's name), and the creator/processor
    // users (so the response can show usernames instead of raw ids) —
    // .Include loads those related rows alongside the order in one query.
    public async Task<List<OrderResponseDto>> GetAllAsync(
        int? createdByUserId, int? involvingUserId, int callerId, string callerRole)
    {
        // A plain User can only ever see their own orders — force the
        // filter to their own id and drop involvingUserId, regardless of
        // what the request asked for, instead of trusting the client to
        // only ever ask nicely for its own data.
        if (!IsAdminOrHigher(callerRole))
        {
            createdByUserId = callerId;
            involvingUserId = null;
        }

        var query = _context.orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Include(o => o.CreatedByUser)
            .Include(o => o.ProcessedByUser)
            .AsQueryable();

        if (createdByUserId is not null)
        {
            query = query.Where(o => o.CreatedByUserId == createdByUserId);
        }

        if (involvingUserId is not null)
        {
            query = query.Where(o => o.CreatedByUserId == involvingUserId || o.ProcessedByUserId == involvingUserId);
        }

        var orders = await query.ToListAsync();
        return orders.Select(OrderResponseDto.FromEntity).ToList();
    }

    public async Task<OrderResponseDto> GetByIdAsync(int id, int callerId, string callerRole)
    {
        var order = await LoadOrderAsync(id);

        var isOwner = order.CreatedByUserId == callerId || order.ProcessedByUserId == callerId;
        if (!isOwner && !IsAdminOrHigher(callerRole))
        {
            throw new ForbiddenException("You can only view orders you created or processed.");
        }

        return OrderResponseDto.FromEntity(order);
    }

    // CREATE: records the order as Pending — no stock is touched here at
    // all — UNLESS the creator is themselves an Admin/SuperAdmin, in which
    // case it's confirmed immediately (see below). Price is still
    // snapshotted here regardless, since that reflects what was offered at
    // the moment the request was made.
    public async Task<OrderResponseDto> CreateAsync(CreateOrderDto dto, int createdByUserId)
    {
        if (dto.Items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.");
        }

        if (!Enum.TryParse<OrderType>(dto.Type, ignoreCase: true, out var type))
        {
            throw new ArgumentException($"'{dto.Type}' is not a valid order type. Use 'In' or 'Out'.");
        }

        var creator = await _context.users.FindAsync(createdByUserId);
        if (creator is null)
        {
            throw new NotFoundException($"User with id {createdByUserId} was not found.");
        }

        // Load every distinct product referenced by this order in one query
        // (instead of one query per line) and index them by id for quick
        // lookups below.
        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in dto.Items)
        {
            if (!products.ContainsKey(item.ProductId))
            {
                throw new ArgumentException($"Product with id {item.ProductId} does not exist.");
            }
        }

        var orderItems = new List<OrderItem>();
        decimal totalPrice = 0;

        foreach (var item in dto.Items)
        {
            var product = products[item.ProductId];

            // Snapshot the current price onto the line. No stock check or
            // mutation here — that's deferred to ConfirmAsync, since stock
            // availability is only meaningful at the moment it's actually
            // applied, not at the moment it was merely requested.
            var unitPrice = product.Price;

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                Product = product,
                Quantity = item.Quantity,
                UnitPrice = unitPrice
            });

            totalPrice += item.Quantity * unitPrice;
        }

        var order = new Order
        {
            Type = type,
            // Status defaults to Pending on the entity itself.
            OrderDate = DateTime.UtcNow,
            TotalPrice = totalPrice,
            CreatedByUserId = createdByUserId,
            // Set directly (we already fetched it above) rather than
            // relying on EF Core to load it — this Order isn't coming from
            // a query with .Include, so the navigation property would
            // otherwise be null and OrderResponseDto.FromEntity would throw.
            CreatedByUser = creator,
            OrderItems = orderItems
        };

        // An Admin/SuperAdmin creating an order would just have to turn
        // around and Confirm their own request anyway (they're already
        // trusted to confirm anyone's) — skip that redundant self-approval
        // step and apply the stock effect immediately, same as ConfirmAsync
        // would. A plain User's order still starts Pending as before.
        if (creator.Role == UserRole.Admin || creator.Role == UserRole.SuperAdmin)
        {
            ApplyStock(order);
            order.Status = OrderStatus.Confirmed;
            order.ProcessedByUserId = createdByUserId;
        }

        _context.orders.Add(order);
        await _context.SaveChangesAsync();

        return OrderResponseDto.FromEntity(order);
    }

    // CONFIRM: Admin-only. This is where stock actually changes for the
    // first time — In adds, Out subtracts (rejecting if any product doesn't
    // have enough stock *right now*, which may differ from when the order
    // was first created).
    public async Task<OrderResponseDto> ConfirmAsync(int id, int actingUserId, string actingRole)
    {
        var order = await LoadOrderAsync(id);
        EnsureActingAdmin(actingRole);

        if (order.Status != OrderStatus.Pending)
        {
            throw new ConflictException($"Order is {order.Status} and cannot be confirmed.");
        }

        ApplyStock(order);

        order.Status = OrderStatus.Confirmed;
        order.ProcessedByUserId = actingUserId;

        await _context.SaveChangesAsync();
        return OrderResponseDto.FromEntity(order);
    }

    // COMPLETE: Admin-only, and only once Confirmed. Just finalizes the
    // order — no stock change, because the stock effect already happened
    // back in ConfirmAsync.
    public async Task<OrderResponseDto> CompleteAsync(int id, int actingUserId, string actingRole)
    {
        var order = await LoadOrderAsync(id);
        EnsureActingAdmin(actingRole);

        if (order.Status != OrderStatus.Confirmed)
        {
            throw new ConflictException($"Order is {order.Status} and cannot be completed — it must be Confirmed first.");
        }

        order.Status = OrderStatus.Completed;
        order.ProcessedByUserId = actingUserId;

        await _context.SaveChangesAsync();
        return OrderResponseDto.FromEntity(order);
    }

    // CANCEL: Admin-only, and only once Confirmed. Reverses whatever stock
    // change ConfirmAsync applied: an In order gave stock, so cancelling
    // takes it back (rejecting if that would go negative); an Out order took
    // stock, so cancelling gives it back.
    public async Task<OrderResponseDto> CancelAsync(int id, int actingUserId, string actingRole)
    {
        var order = await LoadOrderAsync(id);
        EnsureActingAdmin(actingRole);

        if (order.Status != OrderStatus.Confirmed)
        {
            throw new ConflictException($"Order is {order.Status} and cannot be cancelled — it must be Confirmed first.");
        }

        ReverseStock(order, "cancel");

        order.Status = OrderStatus.Cancelled;
        order.ProcessedByUserId = actingUserId;

        await _context.SaveChangesAsync();
        return OrderResponseDto.FromEntity(order);
    }

    // DELETE: not in the spec — added for storage/cleanup purposes only.
    // Same rule as everything else in this project: no direct stock edits
    // outside of order processing. Stock only ever gets applied at Confirm,
    // so: a Pending order never touched stock, deleting it just removes the
    // record; a Confirmed order did have its stock effect applied, so
    // deleting it reverses that first, exactly like Cancel. Completed/
    // Cancelled orders' stock effects are already final/reversed either way.
    public async Task DeleteAsync(int id, string actingRole)
    {
        var order = await LoadOrderAsync(id);
        EnsureActingAdmin(actingRole);

        if (order.Status == OrderStatus.Confirmed)
        {
            ReverseStock(order, "delete");
        }

        // OrderItem -> Order is configured Cascade in AppDbContext, so
        // removing the order also removes its item rows automatically.
        _context.orders.Remove(order);
        await _context.SaveChangesAsync();
    }

    // Shared by ConfirmAsync: applies this order's stock effect for the
    // first time — In adds, Out subtracts. The insufficient-stock check
    // happens here (not at Create) since availability is only meaningful at
    // the moment it's actually applied — stock may have changed since the
    // order was first requested. Validates every line before changing
    // anything, so a rejection never leaves some products already changed.
    private void ApplyStock(Order order)
    {
        if (order.Type == OrderType.Out)
        {
            foreach (var item in order.OrderItems)
            {
                if (item.Product.Quantity < item.Quantity)
                {
                    throw new ConflictException(
                        $"Insufficient stock for '{item.Product.Name}': requested {item.Quantity}, only {item.Product.Quantity} available.");
                }
            }
        }

        foreach (var item in order.OrderItems)
        {
            try
            {
                checked
                {
                    item.Product.Quantity += order.Type == OrderType.In ? item.Quantity : -item.Quantity;
                }
            }
            catch (OverflowException)
            {
                throw new ConflictException(
                    $"Cannot confirm: applying this order would overflow '{item.Product.Name}' past the maximum representable stock value.");
            }

            item.Product.UpdatedAt = DateTime.UtcNow;
        }
    }

    // Shared by CancelAsync and DeleteAsync: reverses whatever stock change
    // ConfirmAsync applied for this order — In gave stock, so reversing
    // takes it back (rejecting if that would go negative); Out took stock,
    // so reversing gives it back (always safe). Validates every line before
    // changing anything, so a negative-stock rejection never leaves some
    // products already reversed and others not.
    private void ReverseStock(Order order, string action)
    {
        if (order.Type == OrderType.In)
        {
            foreach (var item in order.OrderItems)
            {
                if (item.Product.Quantity - item.Quantity < 0)
                {
                    throw new ConflictException(
                        $"Cannot {action}: reversing this order would make '{item.Product.Name}' stock negative.");
                }
            }
        }

        foreach (var item in order.OrderItems)
        {
            try
            {
                checked
                {
                    item.Product.Quantity += order.Type == OrderType.In ? -item.Quantity : item.Quantity;
                }
            }
            catch (OverflowException)
            {
                throw new ConflictException(
                    $"Cannot {action}: reversing this order would overflow '{item.Product.Name}' past the maximum representable stock value.");
            }

            item.Product.UpdatedAt = DateTime.UtcNow;
        }
    }

    // Shared by GetByIdAsync/ConfirmAsync/CompleteAsync/CancelAsync/
    // DeleteAsync: load one order with its items, each item's product, and
    // the creator/processor users, or throw NotFoundException.
    private async Task<Order> LoadOrderAsync(int id)
    {
        var order = await _context.orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Include(o => o.CreatedByUser)
            .Include(o => o.ProcessedByUser)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            throw new NotFoundException($"Order with id {id} was not found.");
        }

        return order;
    }

    // Shared by ConfirmAsync/CompleteAsync/CancelAsync/DeleteAsync: confirm
    // the acting role (read straight from the caller's validated JWT, not a
    // client-supplied field) is at least an Admin. The controller's
    // [Authorize(Roles = "Admin,SuperAdmin")] already enforces this before
    // the request reaches here — this is a defense-in-depth check at the
    // service boundary, same pattern used by ProductService/UserService.
    private static void EnsureActingAdmin(string actingRole)
    {
        if (!IsAdminOrHigher(actingRole))
        {
            throw new ForbiddenException("Only an Admin can confirm, complete, cancel, or delete an order.");
        }
    }

    private static bool IsAdminOrHigher(string role) =>
        Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed)
        && (parsed == UserRole.Admin || parsed == UserRole.SuperAdmin);
}
