using CIS.Application.Hypermedia;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace CIS.Tests.Hypermedia
{
    public class PaginationLinkFactoryTests
    {
        [Fact]
        public void Create_MiddlePage_ReturnsCorrectLinks()
        {
            // Arrange
            int page = 2;
            int totalPages = 3;

            // Act
            var links = PaginationLinkFactory.Create(
                page,
                totalPages,
                targetPage =>
                    $"http://localhost:5039/api/topics?page={targetPage}&pageSize=2"
            );

            // Assert
            Assert.Equal(
                "http://localhost:5039/api/topics?page=2&pageSize=2",
                links.Self);

            Assert.Equal(
                "http://localhost:5039/api/topics?page=1&pageSize=2",
                links.First);

            Assert.Equal(
                "http://localhost:5039/api/topics?page=1&pageSize=2",
                links.Previous);

            Assert.Equal(
                "http://localhost:5039/api/topics?page=3&pageSize=2",
                links.Next);

            Assert.Equal(
                "http://localhost:5039/api/topics?page=3&pageSize=2",
                links.Last);
        }
        [Fact]
        public void Create_FirstPage_ReturnsNextWithoutPrevious()
        {
            // Act
            var links = PaginationLinkFactory.Create(
                1,
                3,
                page => $"http://localhost/api/topics?page={page}"
            );

            // Assert
            Assert.Equal(
                "http://localhost/api/topics?page=1",
                links.Self);

            Assert.Equal(
                "http://localhost/api/topics?page=1",
                links.First);

            Assert.Null(links.Previous);

            Assert.Equal(
                "http://localhost/api/topics?page=2",
                links.Next);

            Assert.Equal(
                "http://localhost/api/topics?page=3",
                links.Last);
        }
        [Fact]
        public void Create_LastPage_ReturnsPreviousWithoutNext()
        {
            // Act
            var links = PaginationLinkFactory.Create(
                3,
                3,
                page => $"http://localhost/api/topics?page={page}"
            );

            // Assert
            Assert.Equal(
                "http://localhost/api/topics?page=3",
                links.Self);

            Assert.Equal(
                "http://localhost/api/topics?page=1",
                links.First);

            Assert.Equal(
                "http://localhost/api/topics?page=2",
                links.Previous);

            Assert.Null(links.Next);

            Assert.Equal(
                "http://localhost/api/topics?page=3",
                links.Last);
        }
        [Fact]
        public void Create_EmptyCollection_ReturnsOnlySelf()
        {
            // Act
            var links = PaginationLinkFactory.Create(
                1,
                0,
                page => $"http://localhost/api/topics?page={page}"
            );

            // Assert
            Assert.Equal(
                "http://localhost/api/topics?page=1",
                links.Self);

            Assert.Null(links.First);
            Assert.Null(links.Previous);
            Assert.Null(links.Next);
            Assert.Null(links.Last);
        }
    }
}
