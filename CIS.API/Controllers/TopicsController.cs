using CIS.Application.Topics;
using CIS.Application.Topics.CreateTopic;
using CIS.Application.Topics.GetTopic;
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
        private readonly IGetTopicService _getTopicService;

        public TopicsController(
            ICreateTopicService createTopicService,
            IGetTopicService getTopicService)
        {
            _createTopicService = createTopicService;
            _getTopicService = getTopicService;
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
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(TopicResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        {
            var topic = await _getTopicService.GetByIdAsync(id, cancellationToken);
            if (topic is null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Topic not found",
                    Detail = $"Topic {id} does not exist."
                });
            }
            return Ok(topic);
        }
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<TopicResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var topics = await _getTopicService.GetAllAsync(cancellationToken);
            return Ok(topics);
        }
    }
}
