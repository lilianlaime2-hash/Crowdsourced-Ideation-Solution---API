using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Application.Exceptions;
using CIS.Application.Hypermedia;
using CIS.Application.Topics;
using CIS.Application.Topics.BrowseTopics;
using CIS.Application.Topics.CreateTopic;
using CIS.Application.Topics.DeleteTopic;
using CIS.Application.Topics.GetTopic;
using CIS.Application.Topics.UpdateTopic;
using CIS.Domain.Enums;
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
        private readonly IUpdateTopicService _updateTopicService;
        private readonly IDeleteTopicService _deleteTopicService;

        public TopicsController(
            ICreateTopicService createTopicService,
            IGetTopicService getTopicService,
            IUpdateTopicService updateTopicService,
            IDeleteTopicService deleteTopicService)
        {
            _createTopicService = createTopicService;
            _getTopicService = getTopicService;
            _updateTopicService = updateTopicService;
            _deleteTopicService = deleteTopicService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(TopicResponse),StatusCodes.Status201Created)]
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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = PaginationRules.DefaultPage,
            [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
            [FromQuery] TopicStatus? status = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? tag = null,
            [FromQuery] string? search = null,
            [FromQuery] TopicSortField sortBy = TopicSortField.CreatedAt,
            [FromQuery] SortDirection sortDirection = SortDirection.Desc,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var options = new TopicBrowseOptions
                {
                    Page = page,
                    PageSize = pageSize,
                    Status = status,
                    UserId = userId,
                    Tag = tag,
                    Search = search,
                    SortBy = sortBy,
                    SortDirection = sortDirection
                };

                var response = await _getTopicService.BrowseAsync(
                    options,
                    cancellationToken);

                var links = PaginationLinkFactory.Create(
                    response.Page,
                    response.TotalPages,
                    targetPage => Url.ActionLink(
                        action: nameof(GetAll),
                        controller: "Topics",
                        values: new
                        {
                            page = targetPage,
                            pageSize = response.PageSize,
                            status,
                            userId,
                            tag,
                            search,
                            sortBy,
                            sortDirection
                        }
                    )
                );

                return Ok(response.WithLinks(links));
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
       
         [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(TopicResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] 
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            long id,
            [FromBody] UpdateTopicRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _updateTopicService.ExecuteAsync(id, request, cancellationToken);

                if (updated is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Topic not found",
                        Detail = $"Topic {id} does not exist."
                    });
                }

                return Ok(updated);
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
            catch (ForbiddenOperationException ex) 
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = ex.Message
                });
            }
        }

        [HttpDelete("{id:long}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _deleteTopicService.ExecuteAsync(id, cancellationToken);

                if (!deleted)
                {
                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Topic not found",
                        Detail = $"Topic {id} does not exist"
                    };

                    return NotFound(problem);
                }
                return NoContent();

            }
            catch (UnauthorizedAccessException)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Authentication required"
                };

                return Unauthorized(problem);
            }
        }
    }
}
