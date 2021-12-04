using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace query_builder
{
    public class QueryTests
    {
        private readonly ITestOutputHelper _outputHelper;
        private NpgsqlConnectionStringBuilder _npgsqlConnectionBuilder;

        public QueryTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;

            _npgsqlConnectionBuilder = new NpgsqlConnectionStringBuilder();
            _npgsqlConnectionBuilder.Host = "127.0.0.1";
            _npgsqlConnectionBuilder.Port = 5432;
            _npgsqlConnectionBuilder.Database = "test";
            _npgsqlConnectionBuilder.Username = "postgres";
            _npgsqlConnectionBuilder.Password = "password";
        }

        /// <summary>
        /// This shows a basic use case for the QueryBuilder class - no checks in this test, but it will output a query to the console.
        /// </summary>
        [Fact]
        public void OutputWrittenQueryToConsole()
        {
            QueryBuilder query = new QueryBuilder()
                .SelectFrom<Table1>()
                .InnerJoin<Table2>("table_1_id")
                .Limit(10)
                .Offset(5)
                .OrderBy<Table1>("created_date", Order.DESCENDING)
                .Return<ReturnClass>();

            _outputHelper.WriteLine(query.AllFieldsQuery());
        }

        /// <summary>
        /// Constructs a query using QueryBuilder, and attempts to get list from Database.
        /// </summary>
        [Fact]
        public async Task SelectListUsingQuery()
        {
            using IDbConnection connection = new NpgsqlConnection(_npgsqlConnectionBuilder.ConnectionString);
            connection.Open();
            QueryBuilder query = new QueryBuilder()
                .SelectFrom<Table1>()
                .Limit(10)
                .Offset(5)
                .OrderBy<Table1>("created_date", Order.DESCENDING);

            IEnumerable<Table1> resultList = await new DatabaseRepository().GetList<Table1>(query, connection);

            connection.Close();
            Assert.NotEmpty(resultList);
        }

        /// <summary>
        /// Constructs a query using QueryBuilder, and attempts to get a PagedListResponse.
        /// </summary>
        [Fact]
        public async Task GetPagedListResponse()
        {
            using IDbConnection connection = new NpgsqlConnection(_npgsqlConnectionBuilder.ConnectionString);
            connection.Open();
            QueryBuilder query = new QueryBuilder()
                .SelectFrom<Table1>()
                .OrderBy<Table1>("created_date", Order.DESCENDING);
            PagedListResponse<Table1> response = await new DatabaseRepository().GetPagedListResponse<Table1>(query, connection);

            Assert.NotNull(response);
            Assert.NotEmpty(response.Results);

            connection.Close();
        }

        /// <summary>
        /// Constructs a query using QueryBuilder, and attempts to get a PagedListResponse with converted results.
        /// </summary>
        [Fact]
        public async Task GetPagedListResponseWithConversion()
        {
            using IDbConnection connection = new NpgsqlConnection(_npgsqlConnectionBuilder.ConnectionString);
            connection.Open();
            QueryBuilder query = new QueryBuilder()
                .SelectFrom<Table1>()
                .OrderBy<Table1>("created_date", Order.DESCENDING);
            PagedListResponse<ReturnClass> convertedResponse = await new DatabaseRepository()
                .GetPagedListResponse<Table1, ReturnClass>(
                    query,
                    async table1List => table1List.ConvertAll((table1) => new ReturnClass()
                    {
                        CreatedDate = table1.CreatedDate
                    }),
                    connection
                );
            
            Assert.NotNull(convertedResponse);
            Assert.NotEmpty(convertedResponse.Results);
            
            connection.Close();
        }
    }
}