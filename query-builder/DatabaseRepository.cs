
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace query_builder
{
    public class DatabaseRepository
    {
        /// <summary>
        /// Retrieves a list of <typeparam name="T"></typeparam>> using the provided database connection
        /// </summary>
        /// <param name="query"><see cref="QueryBuilder"/></param>
        /// <param name="connection"><see cref="IDbConnection"/></param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>.</typeparam>
        /// <returns><see cref="IEnumerable{T}"/></returns>
        public async Task<IEnumerable<T>> GetList<T>(QueryBuilder query, IDbConnection connection)
        {
            List<T> resultSet = new List<T>();
            connection.Open();
            try
            {
                resultSet = (await connection.QueryAsync<T>(query.AllFieldsQuery()))?.AsList();
            }
            catch (Exception e)
            {
                resultSet = new List<T>();
            }
            finally
            {
                connection.Close();
            }    
            return resultSet;
        }
        
        /// <summary>
        /// Retrieves a list of <typeparam name="T"></typeparam>> using the provided database connection,
        /// and returns an instance of <see cref="PagedListResponse{T}"/> with results.
        /// </summary>
        /// <param name="query"><see cref="QueryBuilder"/></param>
        /// <param name="connection"><see cref="IDbConnection"/></param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>.</typeparam>
        /// <returns><see cref="PagedListResponse{T}"/></returns>
        public async Task<PagedListResponse<T>> GetPagedListResponse<T>(QueryBuilder query, IDbConnection connection)
        {
            return new PagedListResponse<T>()
            {
                StartAt = query.GetOffSet() ?? 0,
                MaxResults = query.GetLimit() ?? 0,
                Results = await GetList<T>(query, connection)
            };
        }

        /// <summary>
        /// Retrieves a list of <typeparam name="T"></typeparam>> using the provided database connection, and executes <param name="convertResults"></param>
        /// to return an instance of <see cref="PagedListResponse{U}"/> with results.
        /// </summary>
        /// <param name="query"><see cref="QueryBuilder"/></param>
        /// <param name="connection"><see cref="IDbConnection"/></param>
        /// <param name="convertResults">Method to execute to convert results to <see cref="IEnumerable{U}"/></param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>.</typeparam>
        /// <typeparam name="U">Type returned from <param name="convertResults"></param></typeparam>
        /// <returns><see cref="PagedListResponse{U}"/></returns>
        public async Task<PagedListResponse<U>> GetPagedListResponse<T, U>(QueryBuilder query, Func<List<T>, Task<IEnumerable<U>>> convertResults, IDbConnection connection)
        {
            IEnumerable<T> results = await GetList<T>(query, connection);
            return new PagedListResponse<U>()
            {
                StartAt = query.GetOffSet() ?? 0,
                MaxResults = query.GetLimit() ?? 0,
                Results = await convertResults(results?.AsList())
            };
        }
        
        /// <summary>
        /// Executes a query that will return a single string value. Note that this method expects
        /// the <see cref="QueryBuilder"/>'s <see cref="QueryBuilder.AllFieldsQuery"/> to select a single column, and for this query to only
        /// return a single value - use the <see cref="QueryBuilder.SelectFrom{T}(string[])"/> to specify a single column to be returned.
        /// </summary>
        /// <param name="query"><see cref="QueryBuilder"/></param>
        /// <param name="connection"><see cref="IDbConnection"/></param>
        /// <returns>String result of executed query</returns>
        public async Task<string> GetSingleValueString(QueryBuilder query, IDbConnection connection)
        {
            string result = String.Empty;
            result = await connection.ExecuteScalarAsync<string>(query.AllFieldsQuery());
            return result;
        }

        /// <summary>
        /// Executes the <see cref="QueryBuilder"/>'s CountQuery function using the given the provided database connection, and returns
        /// the integer result.
        /// </summary>
        /// <param name="query"><see cref="QueryBuilder"/></param>
        /// <param name="connection"><see cref="IDbConnection"/></param>
        /// <returns>Result of count query</returns>
        public async Task<int?> GetRecordCount(QueryBuilder query, IDbConnection connection)
        {
            int? count = 0;
            count = await connection.ExecuteScalarAsync<int?>(query.CountQuery());
            return count;
        }

        /// <summary>
        /// Executes the <see cref="QueryBuilder"/>'s DistinctCountQuery function using the given the provided database connection, and returns
        /// the integer result.
        /// </summary>
        /// <param name="query"><see cref="QueryBuilder"/></param>
        /// <param name="connection"><see cref="IDbConnection"/></param>
        /// <param name="columnName">column name to provide to DistinctQueryCount</param>
        /// <returns>Integer result of the count query</returns>
        public async Task<int?> GetDistinctRecordCount(QueryBuilder query, IDbConnection connection, string columnName = "id")
        {
            int? count = 0;
            count = await connection.ExecuteScalarAsync<int?>(query.DistinctCountQuery(columnName));
            return count;
        }
    }
}