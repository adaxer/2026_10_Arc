using MediatR;

namespace Lab_CQRS;

public sealed class BankAccountCommandHandler :
    IRequestHandler<OpenAccountCommand, Guid>,
    IRequestHandler<DepositCommand>,
    IRequestHandler<WithdrawCommand>,
    IRequestHandler<CloseAccountCommand>
{
    private readonly EventStore _store;

    public BankAccountCommandHandler(EventStore store)
    {
        _store = store;
    }

    // Implementierung
    public Task<Guid> Handle(OpenAccountCommand request, CancellationToken cancellationToken)
    {
        #if DEBUG
        var accountId = Guid.Parse("eddf6d82-1154-4448-9bfc-b7e2e91a60fb");
        #else
        var accountId = Guid.NewGuid();
        #endif
        new BankAccount(_store, accountId)?.Open(request.InitialBalance);
        return Task.FromResult(accountId);
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

    public Task Handle(CloseAccountCommand request, CancellationToken cancellationToken)
    {
    var account = _store.GetAccountView(request.AccountId)
        ?? throw new KeyNotFoundException("Konto nicht gefunden.");

    account.Close();
    return Task.CompletedTask;
    }
}