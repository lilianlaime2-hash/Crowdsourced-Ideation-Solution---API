using CIS.Application.Ideas.CreateIdea;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIS_WebAPI.Controllers
{
    [ApiController]
    [Route("api/topics/{topicId:long}/ideas")]
    [Authorize]
    public sealed class IdeasController : ControllerBase
    {
        private readonly ICreateIdeaService _createIdeaService;

        public IdeasController(ICreateIdeaService createIdeaService)
        {
            _createIdeaService = createIdeaService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreateIdeaResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create(
            [FromRoute] long topicId,
            [FromBody] CreateIdeaRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _createIdeaService.ExecuteAsync(
                    topicId,
                    request,
                    cancellationToken);

                if (response is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Topic not found",
                        Detail = $"Topic {topicId} does not exist."
                    });
                }

                return StatusCode(
                    StatusCodes.Status201Created,
                    response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid idea information",
                    Detail = ex.Message
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Authentication required"
                });
            }
        }
    }
}
