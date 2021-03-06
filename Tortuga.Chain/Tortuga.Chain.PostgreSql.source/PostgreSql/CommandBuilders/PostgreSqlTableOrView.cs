﻿using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tortuga.Chain.CommandBuilders;
using Tortuga.Chain.Core;
using Tortuga.Chain.Materializers;
using Tortuga.Chain.Metadata;

namespace Tortuga.Chain.PostgreSql.CommandBuilders
{
    /// <summary>
    /// Class PostgreSqlTableOrView
    /// </summary>
    public class PostgreSqlTableOrView : TableDbCommandBuilder<NpgsqlCommand, NpgsqlParameter, PostgreSqlLimitOption>
    {
        private readonly object m_FilterValue;
        private readonly TableOrViewMetadata<PostgreSqlObjectName, NpgsqlDbType> m_Metadata;
        private readonly string m_WhereClause;
        private readonly object m_ArgumentValue;

        private IEnumerable<SortExpression> m_SortExpressions = Enumerable.Empty<SortExpression>();
        private PostgreSqlLimitOption m_LimitOptions;
        private int? m_Skip;
        private int? m_Take;
        private int? m_Seed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgreSqlTableOrView"/> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        public PostgreSqlTableOrView(PostgreSqlDataSourceBase dataSource, PostgreSqlObjectName tableOrViewName) :
            base(dataSource)
        {
            if (tableOrViewName == PostgreSqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_Metadata = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgreSqlTableOrView"/> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        public PostgreSqlTableOrView(PostgreSqlDataSourceBase dataSource, PostgreSqlObjectName tableOrViewName, object filterValue) :
            base(dataSource)
        {
            if (tableOrViewName == PostgreSqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_FilterValue = filterValue;
            m_Metadata = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        public PostgreSqlTableOrView(PostgreSqlDataSourceBase dataSource, PostgreSqlObjectName tableOrViewName, string whereClause, object argumentValue)
            : base(dataSource)
        {
            if (tableOrViewName == PostgreSqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_WhereClause = whereClause;
            m_ArgumentValue = argumentValue;
            m_Metadata = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Prepares the command for execution by generating any necessary SQL.
        /// </summary>
        /// <param name="materializer">The materializer.</param>
        /// <returns>
        /// ExecutionToken&lt;TCommand&gt;.
        /// </returns>
        public override ExecutionToken<NpgsqlCommand, NpgsqlParameter> Prepare(Materializer<NpgsqlCommand, NpgsqlParameter> materializer)
        {
            if (materializer == null)
                throw new ArgumentNullException(nameof(materializer), $"{nameof(materializer)} is null.");

            var sqlBuilder = m_Metadata.CreateSqlBuilder(StrictMode);
            sqlBuilder.ApplyDesiredColumns(materializer.DesiredColumns());

            //Support check
            if (!Enum.IsDefined(typeof(PostgreSqlLimitOption), m_LimitOptions))
                throw new NotSupportedException($"SQL Server does not support limit option {(LimitOptions)m_LimitOptions}");

            //Validation
            if (m_Skip < 0)
                throw new InvalidOperationException($"Cannot skip {m_Skip} rows");

            if (m_Skip > 0 && m_LimitOptions != PostgreSqlLimitOption.Rows)
                throw new InvalidOperationException($"Cannot perform a Skip operation with limit option {m_LimitOptions}");

            if (m_Take <= 0)
                throw new InvalidOperationException($"Cannot take {m_Take} rows");

            if ((m_LimitOptions == PostgreSqlLimitOption.TableSampleBernoulliPercentage || m_LimitOptions == PostgreSqlLimitOption.TableSampleSystemPercentage) && m_SortExpressions.Any())
                throw new InvalidOperationException($"Cannot perform random sampling when sorting.");


            //SQL Generation
            List<NpgsqlParameter> parameters;
            var sql = new StringBuilder();

            sqlBuilder.BuildSelectClause(sql, "SELECT ", null, " FROM " + m_Metadata.Name.ToQuotedString());

            switch (m_LimitOptions)
            {
                case PostgreSqlLimitOption.TableSampleSystemPercentage:
                    sql.Append($" TABLESAMPLE SYSTEM ({m_Take}) ");
                    if (m_Seed.HasValue)
                        sql.Append($"REPEATABLE ({m_Seed}) ");
                    break;

                case PostgreSqlLimitOption.TableSampleBernoulliPercentage:
                    sql.Append($" TABLESAMPLE BERNOULLI ({m_Take}) ");
                    if (m_Seed.HasValue)
                        sql.Append($"REPEATABLE ({m_Seed}) ");
                    break;
            }

            if (m_FilterValue != null)
            {
                sql.Append(" WHERE " + sqlBuilder.ApplyFilterValue(m_FilterValue));
                sqlBuilder.BuildSoftDeleteClause(sql, " AND ", DataSource, null);

                parameters = sqlBuilder.GetParameters();
            }
            else if (!string.IsNullOrWhiteSpace(m_WhereClause))
            {
                sql.Append(" WHERE " + m_WhereClause);
                sqlBuilder.BuildSoftDeleteClause(sql, " AND ", DataSource, null);

                parameters = SqlBuilder.GetParameters<NpgsqlParameter>(m_ArgumentValue);
                parameters.AddRange(sqlBuilder.GetParameters());
            }
            else
            {
                sqlBuilder.BuildSoftDeleteClause(sql, " WHERE ", DataSource, null);
                parameters = sqlBuilder.GetParameters();
            }
            sqlBuilder.BuildOrderByClause(sql, " ORDER BY ", m_SortExpressions, null);

            switch (m_LimitOptions)
            {
                case PostgreSqlLimitOption.Rows:

                    sql.Append(" OFFSET @offset_row_count_expression ROWS ");
                    parameters.Add(new NpgsqlParameter("@offset_row_count_expression", m_Skip ?? 0));

                    if (m_Take.HasValue)
                    {
                        sql.Append(" FETCH NEXT @fetch_row_count_expression ROWS ONLY");
                        parameters.Add(new NpgsqlParameter("@fetch_row_count_expression", m_Take));
                    }

                    break;
            }

            sql.Append(";");

            return new PostgreSqlExecutionToken(DataSource, "Query " + m_Metadata.Name, sql.ToString(), parameters);
        }


        /// <summary>
        /// Adds sorting to the command builder.
        /// </summary>
        /// <param name="sortExpressions">The sort expressions.</param>
        /// <returns></returns>
        public override TableDbCommandBuilder<NpgsqlCommand, NpgsqlParameter, PostgreSqlLimitOption> WithSorting(IEnumerable<SortExpression> sortExpressions)
        {
            if (sortExpressions == null)
                throw new ArgumentNullException(nameof(sortExpressions), $"{nameof(sortExpressions)} is null.");

            m_SortExpressions = sortExpressions;
            return this;
        }

        protected override TableDbCommandBuilder<NpgsqlCommand, NpgsqlParameter, PostgreSqlLimitOption> OnWithLimits(int? skip, int? take, PostgreSqlLimitOption limitOptions, int? seed)
        {
            m_Seed = seed;
            m_Skip = skip;
            m_Take = take;
            m_LimitOptions = limitOptions;
            return this;
        }

        protected override TableDbCommandBuilder<NpgsqlCommand, NpgsqlParameter, PostgreSqlLimitOption> OnWithLimits(int? skip, int? take, LimitOptions limitOptions, int? seed)
        {
            m_Seed = seed;
            m_Skip = skip;
            m_Take = take;
            m_LimitOptions = (PostgreSqlLimitOption)limitOptions;
            return this;
        }


        public new PostgreSqlDataSourceBase DataSource
        {
            get { return (PostgreSqlDataSourceBase)base.DataSource; }
        }
    }
}
