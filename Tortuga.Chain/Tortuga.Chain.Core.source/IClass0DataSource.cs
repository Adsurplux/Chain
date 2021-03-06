using Tortuga.Chain.CommandBuilders;

namespace Tortuga.Chain
{
    /// <summary>
    /// This indicates that the data source provides minimal functionality.
    /// </summary>
    public interface IClass0DataSource
    {

        ///// <summary>
        ///// Create and open a connection.
        ///// </summary>
        ///// <returns>DbConnection.</returns>
        ///// <remarks>The caller is responsible for closing the connection.</remarks>
        //DbConnection CreateConnection();

        ///// <summary>
        ///// Create and open a connection asynchronously.
        ///// </summary>
        ///// <param name="cancellationToken">The cancellation token.</param>
        ///// <returns>Task&lt;DbConnection&gt;.</returns>
        ///// <remarks>The caller is responsible for closing the connection.</remarks>
        //Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a operation based on a raw SQL statement.
        /// </summary>
        /// <param name="sqlStatement">The SQL statement.</param>
        /// <param name="argumentValue">The argument value.</param>
        IMultipleTableDbCommandBuilder Sql(string sqlStatement, object argumentValue);

    }
}
