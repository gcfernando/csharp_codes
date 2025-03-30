using Mediat.Model;
using MediatR;

namespace Mediat.Business.Queries.UserQuery;

public class GetUserQuery : IRequest<User>
{
    public int UserId { get; set; }
}