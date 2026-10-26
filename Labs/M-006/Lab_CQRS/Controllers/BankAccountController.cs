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

    [HttpPost("{accountId:guid}/deposit")]
    public async Task<IActionResult> Deposit(
        Guid accountId,
        [FromBody] AmountRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new DepositCommand(accountId, request.Amount),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{accountId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(
        Guid accountId,
        [FromBody] AmountRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new WithdrawCommand(accountId, request.Amount),
            cancellationToken);

        return NoContent();
    }

    [HttpGet("{accountId:guid}/balance")]
    public async Task<ActionResult<decimal>> GetBalance(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var balance = await _mediator.Send(
            new GetBalanceQuery(accountId),
            cancellationToken);

        return Ok(balance);
    }

    [HttpGet("{accountId:guid}/history")]
    public async Task<ActionResult<AccountEvent[]>> GetHistory(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var history = await _mediator.Send(
            new GetHistoryQuery(accountId),
            cancellationToken);

        return Ok(history);
    }

    [HttpPost("{accountId:guid}/close")]
    public async Task<IActionResult> CloseAccount(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new CloseAccountCommand(accountId),
            cancellationToken);

        return NoContent();
    }
}

public sealed record AmountRequest(decimal Amount);
