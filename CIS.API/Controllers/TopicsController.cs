using CIS.Application.Topics.CreateTopic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIS_WebAPI.Controllers
{
    [ApiController]
    [Route("api/topics")]
    [Authorize]
    public sealed class TopicsController : ControllerBase
    {
        private readonly ICreateTopicService _createTopicService;

        public TopicsController(ICreateTopicService createTopicService)
        {
            _createTopicService = createTopicService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreateTopicResponse),StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _createTopicService.ExecuteAsync(
                        request,
                        cancellationToken);

                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid topic information",
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
