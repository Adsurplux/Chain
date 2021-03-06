﻿using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using Tortuga.Chain.SqlServer;

namespace Tortuga.Chain
{
    /// <summary>
    /// Class SqlServerExtensions.
    /// </summary>
    public static class SqlServerExtensions
    {
        private readonly static ConcurrentDictionary<string, SqlServerDataSource> s_CachedDataSources = new ConcurrentDictionary<string, SqlServerDataSource>();

        /// <summary>
        /// Returns a data source wrapped around the connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns>SqlServerOpenDataSource.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static SqlServerOpenDataSource AsDataSource(this SqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection), $"{nameof(connection)} is null.");
            if (connection.State == ConnectionState.Closed)
                connection.Open();

            var dataSourceBase = s_CachedDataSources.GetOrAdd(connection.ConnectionString, cs => new SqlServerDataSource(cs));
            return new SqlServerOpenDataSource(dataSourceBase, connection, null);
        }

        /// <summary>
        /// Returns a data source wrapped around the transaction.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="transaction">The transaction.</param>
        /// <returns>SqlServerOpenDataSource.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static SqlServerOpenDataSource AsDataSource(this SqlConnection connection, SqlTransaction transaction)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection), $"{nameof(connection)} is null.");
            if (connection.State == ConnectionState.Closed)
                connection.Open();

            var dataSourceBase = s_CachedDataSources.GetOrAdd(connection.ConnectionString, cs => new SqlServerDataSource(cs));
            return new SqlServerOpenDataSource(dataSourceBase, connection, transaction);
        }
    }
}
