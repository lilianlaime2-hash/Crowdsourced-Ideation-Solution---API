using CIS.Application.Ideas.CreateIdea;
using CIS.Application.Ideas.GetIdeas;
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
        private readonly IGetIdeasService _getIdeasService;

        public IdeasController(ICreateIdeaService createIdeaService, IGetIdeasService getIdeasService)
        {
            _createIdeaService = createIdeaService;
            _getIdeasService = getIdeasService;
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
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<CreateIdeaResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByTopic(
            [FromRoute] long topicId,
            CancellationToken cancellationToken)
        {
            var ideas = await _getIdeasService.GetByTopicIdAsync(topicId, cancellationToken);

            if (ideas is null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Topic not found",
                    Detail = $"Topic {topicId} does not exist."
                });
            }

            return Ok(ideas);
        }
    }
}
