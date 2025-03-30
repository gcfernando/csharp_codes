using Mediat.Model;

namespace Mediat.Data.Contract;

public interface IUserRepository
{
    Task<User> GetByIdAsync(int userId);

    Task AddAsync(User user);
}