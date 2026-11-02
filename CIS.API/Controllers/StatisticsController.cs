using CIS.Application.Statistics;
using CIS.Application.Statistics.GetMostVotedIdeas;
using CIS.Application.Statistics.GetMostActiveTopics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIS_WebAPI.Controllers
{
    [ApiController]
    [Route("api/statistics")]
    [Authorize]
    public sealed class StatisticsController : ControllerBase
    {
        private readonly IGetMostVotedIdeasService _getMostVotedIdeasService;
        private readonly IGetMostActiveTopicsService _getMostActiveTopicsService;

        public StatisticsController(IGetMostVotedIdeasService getMostVotedIdeasService,
            IGetMostActiveTopicsService getMostActiveTopicsService)
        {
            _getMostVotedIdeasService = getMostVotedIdeasService;
            _getMostActiveTopicsService = getMostActiveTopicsService;
        }

        [HttpGet("ideas/most-voted")]
        [ProducesResponseType(typeof(PagedResponse<MostVotedIdeaResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMostVotedIdeas(
            [FromQuery] int page = PaginationRules.DefaultPage,
            [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _getMostVotedIdeasService.ExecuteAsync(
                    page,
                    pageSize,
                    cancellationToken);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid pagination parameters",
                    Detail = ex.Message
                });
            }
        }
        [HttpGet("topics/most-active")]
        [ProducesResponseType(typeof(PagedResponse<MostActiveTopicResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMostActiveTopics(
            [FromQuery] int page = PaginationRules.DefaultPage,
            [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _getMostActiveTopicsService.ExecuteAsync(
                    page,
                    pageSize,
                    cancellationToken);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid pagination parameters",
                    Detail = ex.Message
                });
            }
        }
    }
}