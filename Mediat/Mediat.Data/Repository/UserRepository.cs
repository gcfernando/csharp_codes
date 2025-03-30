using Mediat.Data.Contract;
using Mediat.Model;

namespace Mediat.Data.Repository;

public class UserRepository : IUserRepository
{
    private static readonly List<User> _users = [];

    public Task<User> GetByIdAsync(int userId) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));

    public Task AddAsync(User user)
    {
        user.Id = _users.Count + 1;
        _users.Add(user);

        return Task.CompletedTask;
    }
}