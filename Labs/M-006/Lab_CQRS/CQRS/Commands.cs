using MediatR;

namespace Lab_CQRS;

public sealed record OpenAccountCommand(decimal InitialBalance) : IRequest<Guid>;
public sealed record CloseAccountCommand(Guid AccountId) : IRequest;
public sealed record DepositCommand(Guid AccountId, decimal Amount) : IRequest;
public sealed record WithdrawCommand(Guid AccountId, decimal Amount) : IRequest;
