using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskProcessor.Application.Job.Create;
using TaskProcessor.Application.Job.Create.Dtos.Request;
using TaskProcessor.Application.Job.GetById;
using TaskProcessor.Presentation.Extensions;

namespace TaskProcessor.Presentation.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateJobRequestDto request,
        CancellationToken ct)
    {
        var command = new CreateJobCommand(request.Type, request.Payload);
        var result = await sender.Send(command, ct);

        if (result.IsError)
            return result.ToErrorResult();

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id },
            result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var query = new GetJobByIdQuery(id);
        var result = await sender.Send(query, ct);

        if (result.IsError)
            return result.ToErrorResult();

        return Ok(result.Value);
    }
}
