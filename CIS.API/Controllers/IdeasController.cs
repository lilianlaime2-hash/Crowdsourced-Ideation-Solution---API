using CIS.Application.Common.Pagination;
using CIS.Application.Exceptions;
using CIS.Application.Hypermedia;
using CIS.Application.Ideas.CreateIdea;
using CIS.Application.Ideas.DeleteIdea;
using CIS.Application.Ideas.DeleteVote;
using CIS.Application.Ideas.GetIdeas;
using CIS.Application.Ideas.UpdateIdea;
using CIS.Application.Ideas.VoteIdea;
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
        private readonly IUpdateIdeaService _updateIdeaService;
        private readonly IDeleteIdeaService _deleteIdeaService;
        private readonly IVoteIdeaService _voteIdeaService;
        private readonly IDeleteVoteService _deleteVoteService;

        public IdeasController(ICreateIdeaService createIdeaService, IGetIdeasService getIdeasService, IUpdateIdeaService updateIdeaService,
            IDeleteIdeaService deleteIdeaService, IVoteIdeaService voteIdeaService, IDeleteVoteService deleteVoteService)
        {
            _createIdeaService = createIdeaService;
            _getIdeasService = getIdeasService;
            _updateIdeaService = updateIdeaService;
            _deleteIdeaService = deleteIdeaService;
            _voteIdeaService = voteIdeaService;
            _deleteVoteService = deleteVoteService;
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
            [FromQuery] int page = PaginationRules.DefaultPage,
            [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _getIdeasService.GetPagedByTopicIdAsync(
                    topicId,
                    page,
                    pageSize,
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

                var links = PaginationLinkFactory.Create(
                    response.Page,
                    response.TotalPages,
                    targetPage => Url.ActionLink(action: nameof(GetByTopic), controller: "Ideas",
                        values: new
                        {
                            topicId,
                            page = targetPage,
                            pageSize = response.PageSize
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
        [HttpPut("~/api/ideas/{id:long}")]
        [ProducesResponseType(typeof(CreateIdeaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] long id,
            [FromBody] CreateIdeaRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _updateIdeaService.ExecuteAsync(id, request, cancellationToken);

                if (updated is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Idea not found",
                        Detail = $"Idea {id} does not exist."
                    });
                }

                return Ok(updated);
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
        public async Task<IActionResult> Delete(
            [FromRoute] long topicId,
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _deleteIdeaService.ExecuteAsync(topicId, id, cancellationToken);

                if (!deleted)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Idea not found",
                        Detail = $"Idea {id} does not exist in topic {topicId}."
                    });
                }

                return NoContent();
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
        
        
        [HttpPost("{ideaId:long}/votes")]
        [ProducesResponseType(typeof(VoteIdeaResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Vote([FromRoute] long topicId, [FromRoute] long ideaId, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _voteIdeaService.ExecuteAsync(topicId, ideaId, cancellationToken);

                if (response is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Idea not found",
                        Detail = $"Idea {ideaId} does not exist in topic {topicId}."
                    });
                }

                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (ConflictException ex)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Vote already registered",
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

        [HttpDelete("{ideaId:long}/votes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVote([FromRoute] long topicId, [FromRoute] long ideaId, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _deleteVoteService.ExecuteAsync(topicId, ideaId, cancellationToken);

                if (!deleted)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Vote not found",
                        Detail = $"You have not voted for idea {ideaId} in topic {topicId}."
                    });
                }

                return NoContent();
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
