using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Lab_CQRS.Controllers;

[ApiController]
[Route("[controller]")]
public class BankAccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public BankAccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("open")]
    public async Task<ActionResult<Guid>> OpenAccount(
     [FromBody] OpenAccountCommand command,
     CancellationToken cancellationToken)
    {
        var accountId = await _mediator.Send(command, cancellationToken);
        return Ok(accountId);
    }

    [HttpPost("{accountId}/deposit")]
    public async Task<ActionResult> Deposit(
     [FromRoute] Guid accountId,
     [FromBody] AmountRequest request,
     CancellationToken cancellationToken)
    {
        await _mediator.Send(new DepositCommand(accountId, request.Amount), cancellationToken);
        return Ok();
    }

    [HttpPost("{accountId}/withdraw")]
    public async Task<ActionResult> Withdraw(
     [FromRoute] Guid accountId,
     [FromBody] AmountRequest request,
     CancellationToken cancellationToken)
    {
        await _mediator.Send(new WithdrawCommand(accountId,request.Amount), cancellationToken);
        return Ok();
    }

    [HttpPost("{accountId}/close")]
    public async Task<ActionResult> CloseAccount(
     [FromRoute] Guid accountId,
     CancellationToken cancellationToken)
    {
        await _mediator.Send(new CloseAccountCommand(accountId), cancellationToken);
        return Ok();
    }

    [HttpGet("{accountId}/history")]
    public async Task<ActionResult<AccountEvent[]>> GetHistory(
     [FromRoute] Guid accountId,
     CancellationToken cancellationToken)
    {
        var history = await _mediator.Send(new GetHistoryQuery(accountId), cancellationToken);
        return Ok(history);
    }
    
    [HttpGet("{accountId}/balance")]
    public async Task<ActionResult<decimal>> GetBalance(
     [FromRoute] Guid accountId,
     CancellationToken cancellationToken)
    {
        var balance = await _mediator.Send(new GetBalanceQuery(accountId), cancellationToken);
        return Ok(balance);
    }   
}

public sealed record AmountRequest(decimal Amount);
