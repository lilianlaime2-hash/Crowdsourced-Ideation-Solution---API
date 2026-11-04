# Matriz HTTP de los cinco requisitos (Fase 3)

The suite in `CIS.IntegrationTests/Http` contains 99 independently discovered xUnit cases for all 13 CIS method/route operations. Every theory row creates a fresh `cis_integration_<guid>` database in a disposable MongoDB Testcontainer. `CisApiFactory` runs the real API, middleware, services, and MongoDB repositories. JWTs use the factory's test-only HS256 key and a UUID `sub`; only the independent Users API is substituted.

For content, the valid minimum is title/description length **1/1**, maximum **150/1000**, and midpoint **75/500**. For lists, pageSize values are **1**, **100**, and midpoint **50** (the arithmetic midpoint rounded down). 101 persisted rows exercise page windows of 1, 50 and 100 items; aggregate requests also verify counts and ranking against real ideas/votes. Resource IDs are identifiers without a meaningful average or content-length range. Detail, delete, and vote operations therefore use existence, visibility, ownership, and conflict cases, rather than fabricated numeric minima/averages/maxima.

Each row below corresponds to a separately reported test case. Class/method names are relative to namespace `CIS.IntegrationTests.Http`.

| # | Method | Route | Test method | Independent case | Expected and persistence evidence |
|---|---|---|---|---|---|
| 1 | GET | `/api/topics` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `topics,1` minimum | 200; one real topic, total 101 |
| 2 | GET | `/api/topics` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `topics,100` maximum | 200; 100 real topics |
| 3 | GET | `/api/topics` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `topics,50` midpoint | 200; 50 real topics, total 101 |
| 4 | GET | `/api/topics/{id}` | `ResourceEndpointTests.TopicDetail_ExistenceAndVisibility` | `existing` | 200; exact stored ID/title/description |
| 5 | GET | `/api/topics/{id}` | `ResourceEndpointTests.TopicDetail_ExistenceAndVisibility` | `missing` | 404; existing document unchanged |
| 6 | GET | `/api/topics/{id}` | `ResourceEndpointTests.TopicDetail_ExistenceAndVisibility` | `deleted` | 404; soft-deleted record remains persisted |
| 7 | POST | `/api/topics` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `topic,POST,1,1` minimum | 201; exact content and owner persisted |
| 8 | POST | `/api/topics` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `topic,POST,150,1000` maximum | 201; exact content and owner persisted |
| 9 | POST | `/api/topics` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `topic,POST,75,500` midpoint | 201; exact content and owner persisted |
| 10 | PUT | `/api/topics/{id}` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `topic,PUT,1,1` minimum | 200; content updated, timestamp advanced |
| 11 | PUT | `/api/topics/{id}` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `topic,PUT,150,1000` maximum | 200; content updated, timestamp advanced |
| 12 | PUT | `/api/topics/{id}` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `topic,PUT,75,500` midpoint | 200; content updated, timestamp advanced |
| 13 | DELETE | `/api/topics/{id}` | `ResourceEndpointTests.Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete` | `topic,owner` | 204; record retained as Deleted |
| 14 | DELETE | `/api/topics/{id}` | `ResourceEndpointTests.Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete` | `topic,missing` | 404; existing record remains Active |
| 15 | DELETE | `/api/topics/{id}` | `ResourceEndpointTests.Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete` | `topic,other-owner` | 404; owner record remains Active |
| 16 | GET | `/api/topics/{topicId}/ideas` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `ideas,1` minimum | 200; one persisted idea, total 101 |
| 17 | GET | `/api/topics/{topicId}/ideas` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `ideas,100` maximum | 200; 100 persisted ideas |
| 18 | GET | `/api/topics/{topicId}/ideas` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `ideas,50` midpoint | 200; 50 persisted ideas, total 101 |
| 19 | POST | `/api/topics/{topicId}/ideas` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `idea,POST,1,1` minimum | 201; content, owner and topic persisted |
| 20 | POST | `/api/topics/{topicId}/ideas` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `idea,POST,150,1000` maximum | 201; content, owner and topic persisted |
| 21 | POST | `/api/topics/{topicId}/ideas` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `idea,POST,75,500` midpoint | 201; content, owner and topic persisted |
| 22 | PUT | `/api/ideas/{id}` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `idea,PUT,1,1` minimum | 200; exact content updated, timestamp advanced |
| 23 | PUT | `/api/ideas/{id}` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `idea,PUT,150,1000` maximum | 200; exact content updated, timestamp advanced |
| 24 | PUT | `/api/ideas/{id}` | `ContentEndpointTests.Content_ValidBounds_PersistsExactContent` | `idea,PUT,75,500` midpoint | 200; exact content updated, timestamp advanced |
| 25 | DELETE | `/api/topics/{topicId}/ideas/{id}` | `ResourceEndpointTests.Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete` | `idea,owner` | 204; record retained as Deleted |
| 26 | DELETE | `/api/topics/{topicId}/ideas/{id}` | `ResourceEndpointTests.Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete` | `idea,missing` | 404; existing idea remains Active |
| 27 | DELETE | `/api/topics/{topicId}/ideas/{id}` | `ResourceEndpointTests.Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete` | `idea,other-owner` | 404; owner's idea remains Active |
| 28 | POST | `/api/topics/{topicId}/ideas/{ideaId}/votes` | `VoteEndpointTests.Vote_ExistenceAndUniqueness_PersistsOnlyValidVote` | `new` | 201; exact user/idea vote persisted |
| 29 | POST | `/api/topics/{topicId}/ideas/{ideaId}/votes` | `VoteEndpointTests.Vote_ExistenceAndUniqueness_PersistsOnlyValidVote` | `duplicate` | 409; exactly one original vote remains |
| 30 | POST | `/api/topics/{topicId}/ideas/{ideaId}/votes` | `VoteEndpointTests.Vote_ExistenceAndUniqueness_PersistsOnlyValidVote` | `wrong-topic` | 404; no vote inserted |
| 31 | DELETE | `/api/topics/{topicId}/ideas/{ideaId}/votes` | `VoteEndpointTests.DeleteVote_ExistenceAndOwnership_PreservesOtherUsersVote` | `own-vote` | 204; own vote removed, other user's retained |
| 32 | DELETE | `/api/topics/{topicId}/ideas/{ideaId}/votes` | `VoteEndpointTests.DeleteVote_ExistenceAndOwnership_PreservesOtherUsersVote` | `no-vote` | 404; empty vote collection remains empty |
| 33 | DELETE | `/api/topics/{topicId}/ideas/{ideaId}/votes` | `VoteEndpointTests.DeleteVote_ExistenceAndOwnership_PreservesOtherUsersVote` | `other-user-vote` | 404; other user's vote retained |
| 34 | GET | `/api/statistics/ideas/most-voted` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `most-voted,1` minimum | 200; top idea has two real votes; total 101 |
| 35 | GET | `/api/statistics/ideas/most-voted` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `most-voted,100` maximum | 200; 100 persisted ideas; correct top rank |
| 36 | GET | `/api/statistics/ideas/most-voted` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `most-voted,50` midpoint | 200; 50 items; correct top rank and total |
| 37 | GET | `/api/statistics/topics/most-active` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `most-active,1` minimum | 200; top topic has 101 ideas,102 votes,score203 |
| 38 | GET | `/api/statistics/topics/most-active` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `most-active,100` maximum | 200; 100 topics; correct aggregate counts |
| 39 | GET | `/api/statistics/topics/most-active` | `BrowseEndpointTests.Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals` | `most-active,50` midpoint | 200; 50 topics; correct aggregate counts |

Additional independently reported cases extend the matrix to 99:

| Test method | Additional cases | Count |
|---|---|---|
| `Content_InvalidBounds_Returns400WithoutMutation` | Each topic/idea POST/PUT: empty title,151-char title,empty description,1001-char description; 400 with contents/timestamps/counts unchanged | 16 |
| `Update_OwnershipAndExistence_DoNotMutate` | Each topic/idea PUT: other owner403,missing404 | 4 |
| `CreateIdea_MissingTopic_Returns404WithoutInsertion` | Idea POST missing topic404, no idea inserted | 1 |
| `Browse_InvalidPageSize_Returns400` | Each of four GET lists: pageSize0,101,-1 | 12 |
| `Browse_PageZero_Returns400` | Each of four GET lists: page0 | 4 |
| `BrowseIdeas_MissingTopic_Returns404` | Idea GET list missing parent404 | 1 |
| `TopicDetail_ExistenceAndVisibility` | Other authenticated reader can read persisted topic | 1 |
| `Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete` | Topic/idea already deleted; idea wrong parent; all404 without change | 3 |
| `Vote_ExistenceAndUniqueness_PersistsOnlyValidVote` | Another user's first vote201,missing idea404,deleted idea404 | 3 |
| `DeleteVote_ExistenceAndOwnership_PreservesOtherUsersVote` | Wrong parent404,missing idea404; both votes remain | 2 |
| `Endpoint_MissingJwt_Returns401WithoutMutation` | One row for each of the13 method/route operations above; real middleware401, contents/status/timestamps/counts unchanged | 13 |

The earlier 14 integration tests are preserved separately; the HTTP filter selects only the new 99 cases. No developer or production database is used.

Run from the repository root:

```powershell
dotnet test CIS.IntegrationTests/CIS.IntegrationTests.csproj --filter 'FullyQualifiedName~CIS.IntegrationTests.Http' --logger 'console;verbosity=minimal'
```

Docker must be available for the existing Testcontainers fixture. The test runner may require access outside the restricted Windows sandbox because the .NET SDK inspects Windows services and Testcontainers contacts Docker.
