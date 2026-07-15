// Abstraction over password hashing so UserService never touches raw
// cryptography calls directly — same reasoning as every other interface in
// this project (IUserService, IProductService, ...): callers depend on the
// contract, not the implementation.
namespace db.Service.Auth;

public interface IPasswordHasher
{
    // Produces a new, self-describing hash string (includes the salt and
    // iteration count) safe to store directly in the Password column.
    string Hash(string password);

    // Verifies a plain-text password against a previously hashed value.
    bool Verify(string password, string storedValue);

    // True if storedValue is already in our hash format. Used by
    // UserService.LoginAsync to tell a real hash apart from one of the
    // legacy plaintext passwords seeded before this feature existed.
    bool IsHashed(string storedValue);
}
