using MediatR;

namespace Lab_CQRS;

public sealed record GetBalanceQuery(Guid AccountId) : IRequest<decimal>;
public sealed record GetHistoryQuery(Guid AccountId) : IRequest<AccountEvent[]>;
