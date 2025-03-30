using Mediat.Model;
using MediatR;

namespace Mediat.Business.Commands.UserCommand;

public class CreateUserCommand : IRequest<User>
{
    public string Name { get; set; }
    public string Email { get; set; }
}