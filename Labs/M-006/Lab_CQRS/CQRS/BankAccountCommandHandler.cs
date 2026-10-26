using MediatR;

namespace Lab_CQRS;

public sealed class BankAccountCommandHandler : IRequestHandler<OpenAccountCommand, Guid>,
    IRequestHandler<DepositCommand>,
    IRequestHandler<WithdrawCommand>,
    IRequestHandler<CloseAccountCommand>
{
    private readonly EventStore _store;

    public BankAccountCommandHandler(EventStore store)
    {
        _store = store;
    }

    public Task Handle(
        DepositCommand request,
        CancellationToken cancellationToken)
    {
        var account = _store.GetAccountView(request.AccountId)
            ?? throw new KeyNotFoundException("Konto nicht gefunden.");

        account.Deposit(request.Amount);
        return Task.CompletedTask;
    }

    public Task Handle(WithdrawCommand request, CancellationToken cancellationToken)
    {
        var account = _store.GetAccountView(request.AccountId)
            ?? throw new KeyNotFoundException("Konto nicht gefunden.");

        account.Withdraw(request.Amount);
        return Task.CompletedTask;
    }

    public Task<Guid> Handle(OpenAccountCommand request, CancellationToken cancellationToken)
    {
        var accountId = Guid.NewGuid();
        new BankAccount(_store, accountId).Open(request.InitialBalance);
        return Task.FromResult(accountId);
    }

    public Task Handle(CloseAccountCommand request, CancellationToken cancellationToken)
    {
        var account = _store.GetAccountView(request.AccountId)
            ?? throw new KeyNotFoundException("Konto nicht gefunden.");

        account.Close();
        return Task.CompletedTask;
    }
}
