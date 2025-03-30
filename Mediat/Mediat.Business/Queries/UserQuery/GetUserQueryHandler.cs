using Mediat.Data.Contract;
using Mediat.Model;
using MediatR;

namespace Mediat.Business.Queries.UserQuery;

public class GetUserQueryHandler(IUserRepository userRepository) : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _userRepository = userRepository;

    public Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken) =>
        _userRepository.GetByIdAsync(request.UserId);
}