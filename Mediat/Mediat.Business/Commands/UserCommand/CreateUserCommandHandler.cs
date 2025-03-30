using Mediat.Data.Contract;
using Mediat.Model;
using MediatR;

namespace Mediat.Business.Commands.UserCommand;

public class CreateUserCommandHandler(IUserRepository userRepository) : IRequestHandler<CreateUserCommand, User>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<User> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Name = request.Name,
            Email = request.Email
        };

        await _userRepository.AddAsync(user);
        return user;
    }
}