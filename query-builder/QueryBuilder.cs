using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace query_builder
{
    public class QueryBuilder
    {
        private TableDetail _mainTable;
        private List<JoinTableDetail> _joinTableDetails = new();
        private List<string> whereConditionsList = new();
        private string _distinctOn;
        private string _orderBy;
        private int? _limit;
        private int? _offset;
        private List<TableFieldDetail> _fieldsList = new();
        private List<TableFieldDetail> _returnFieldsList;
        private Dictionary<string, List<TableFieldDetail>> _joinFields = new();

        ///
        /// <summary>
        /// Sets the first table to select from when query is generated.
        /// <para>
        /// This method will attempt to initiate the table details to select from, as well as initialize the fields to be selected.<br></br>
        /// </para>
        /// </summary>
        /// <typeparam name="T">Class marked with <see cref="TableAttribute"/></typeparam>
        /// <returns>This instance</returns>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="T"/> is not marked with <see cref="TableAttribute"/></exception>
        public QueryBuilder SelectFrom<T>()
        {
            InitMainTable(typeof(T));
            InitFields(_mainTable.TableType, _mainTable.TableAlias);
            return this;
        }

        /// <summary>
        /// Sets the first table to select from when query is generated, and sets the fields to be selected.<br></br>
        /// Use this if you're expecting a big result set and want to optimize query by only selecting the necessary fields.
        /// </summary>
        /// <typeparam name="T">Class marked with <see cref="TableAttribute"/></typeparam>
        /// <param name="fieldsToSelect">A list of strings with valid column names to select</param>
        /// <returns>This instance</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <typeparamref name="T"/> is not marked with <see cref="TableAttribute"/>, if item in fieldsToSelect does not exist on <typeparamref name="T"/>
        /// or if <typeparamref name="T"/> has no fields with attribute <see cref="ColumnAttribute"/>.
        /// </exception>
        public QueryBuilder SelectFrom<T>(params string[] fieldsToSelect)
        {
            TableDetail tableDetails = InitMainTable(typeof(T));
            foreach (var fieldName in fieldsToSelect)
            {
                if (!ColumnNameExists(tableDetails?.TableType, fieldName))
                    throw new ArgumentException($"{fieldName} does not exist on table {tableDetails?.TableName}");

                var prop = tableDetails?.TableType.GetProperties()
                    .FirstOrDefault(p =>
                        (Attribute.GetCustomAttribute(p, typeof(ColumnAttribute)) as ColumnAttribute)?.Name?.Equals(fieldName) ?? false);

                if (prop == null)
                    throw new ArgumentException($"{typeof(T)} does not have any fields marked with ColumnAttribute");

                _fieldsList.Add(new TableFieldDetail()
                {
                    ColumnName = fieldName,
                    ColumnAlias = prop.Name,
                    TableAlias = tableDetails.TableAlias
                });
            }

            return this;
        }

        private TableDetail InitMainTable(Type type)
        {
            var attributes = type.GetCustomAttributes(typeof(TableAttribute), true);

            if (attributes.Length == 0)
                throw new ArgumentException($"{type} does not have attribute TableAttribute");

            var tableAttr = (TableAttribute)attributes.GetValue(0);
            _mainTable = new TableDetail()
            {
                TableName = tableAttr?.Name,
                TableType = type,
                TableAlias = $"{tableAttr?.Name.Substring(0, 2)}1"
            };
            return _mainTable;
        }

        private void InitFields(Type type, string tableAlias)
        {
            var props = type?.GetProperties();
            if (props != null)
            {
                foreach (var prop in props)
                {
                    _fieldsList.Add(new TableFieldDetail()
                    {
                        ColumnName = (Attribute.GetCustomAttribute(prop, typeof(ColumnAttribute)) as ColumnAttribute)?.Name,
                        ColumnAlias = prop.Name,
                        TableAlias = tableAlias
                    });
                }
            }
        }

        private bool ColumnNameExists(Type type, string fieldName)
        {
            var props = type?.GetProperties();

            int castIndex = fieldName.IndexOf("::");
            if (castIndex > 0)
            {
                fieldName = fieldName.Substring(0, castIndex);
            }

            string columnName = props?.First(p =>
                (Attribute.GetCustomAttribute(p, typeof(ColumnAttribute)) as ColumnAttribute)?.Name?.Equals(fieldName) ?? false)?.Name;
            return !columnName.IsNullOrEmpty();
        }

        /// <summary>
        /// Specify the type that should be returned when this query is generated.
        /// <para>
        /// By default - the query will return all fields for the type supplied with the <see> <cref>SelectFrom{T}</cref> </see> method.
        /// If, however, a different return object is needed - this method may be used to specify.
        /// </para>
        /// <para>
        /// Although <typeparamref name="T"/> does not need the <see cref="TableAttribute"/>, this method still expects fields to be marked with <see cref="ColumnAttribute"/>.
        /// Additionally, if ambiguous column names exist on the tables used in this query, mark the field with <see cref="QueryTableAttribute"/> to specify the table that
        /// should be used to populate that field. If the table specified in the attribute is not in the query, the <see cref="QueryBuilder"/> will find the first column
        /// name to match in the fields of all the tables.<br></br>
        /// Preference is given to the primary table specified with the <see> <cref>SelectFrom{T}</cref> </see> method -
        /// so if multiple tables have the same name, and the <see cref="QueryTableAttribute"/> is not used, the column from the primary table will be used.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder Return<T>()
        {
            _joinTableDetails.ForEach(ta => InitFields(ta.TableType, ta.TableAlias));
            _returnFieldsList = new List<TableFieldDetail>();
            var props = typeof(T)?.GetProperties();
            foreach (var prop in props)
            {
                TableFieldDetail detail = null;
                string columnName = (Attribute.GetCustomAttribute(prop, typeof(ColumnAttribute)) as ColumnAttribute)?.Name;
                Type table = (Attribute.GetCustomAttribute(prop, typeof(QueryTableAttribute)) as QueryTableAttribute)?.table;
                if (table != null)
                {
                    TableDetail td = _mainTable.TableType.Equals(table) ? _mainTable : _joinTableDetails.Find(t => t.TableType.Equals(table));
                    detail = _fieldsList.Find(f => f.ColumnName.Equals(columnName) && f.TableAlias.Equals(td?.TableAlias));
                }

                if (detail == null)
                {
                    // if it exists on main table, use main table...
                    bool exists = _fieldsList.Exists(fs => fs.ColumnName.Equals(columnName) && fs.TableAlias.Equals(_mainTable.TableAlias));
                    detail = exists
                        ? _fieldsList.Find(f => f.ColumnName.Equals(columnName) && f.TableAlias.Equals(_mainTable.TableAlias))
                        : _fieldsList.Find(f => f.ColumnName.Equals(columnName));
                }

                if (detail == null)
                {
                    throw new ArgumentException($"{columnName} does not exist on any tables defined in this query");
                }

                _returnFieldsList.Add(new TableFieldDetail()
                {
                    ColumnName = columnName,
                    ColumnAlias = prop.Name,
                    TableAlias = detail.TableAlias
                });
            }

            return this;
        }

        /// <summary>
        /// Specify a distinct column for the select - will generate SQL like <c>SELECT DISTINCT ON(distinctOn)...</c>.
        /// </summary>
        /// <param name="distinctOn">column name for distinct values</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder DistinctOn<T>(string distinctOn)
        {
            var tableDetails = _mainTable.TableType == typeof(T) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(T));
            if (tableDetails == null)
                throw new ArgumentException($"{typeof(T)} has not been added to query.");

            var attributes = typeof(T).GetCustomAttributes(typeof(TableAttribute), true);

            if (attributes.Length == 0)
                throw new ArgumentException("T does not have attribute TableAttribute");

            var tableAttr = (TableAttribute)attributes.GetValue(0);
            if (tableAttr == null) throw new ArgumentException("Type does not have a TableAttribute");

            _distinctOn = $"{tableDetails.TableAlias}.{distinctOn}";
            return this;
        }

        /// <summary>
        /// Adds an inner join to the main table's primary key column (default being <c>id<c/>).<br></br>
        /// This is a shortened override to make it possible to specify only the join table, and the foreign key column
        /// to link to the main table.
        /// </summary>
        /// <param name="t_on">FK column on <typeparamref name="T"/></param>
        /// <param name="mainOn">Optional - column name for main table's primary key</param>
        /// <typeparam name="T">Class marked with <see cref="TableAttribute"/></typeparam>
        /// <returns>This instance</returns>
        public QueryBuilder InnerJoin<T>(string t_on, string mainOn = "id")
        {
            return InnerJoin<T>(t_on, mainOn, _mainTable);
        }

        /// <summary>
        /// Adds an inner join to the <typeparamref name="T"/> table's primary/foreign key column.<br></br>
        /// 
        /// </summary>
        /// <param name="t_on">column on <typeparamref name="T"/></param>
        /// <param name="u_on">column name for <typeparamref name="U"/></param>
        /// <typeparam name="T">Class marked with <see cref="TableAttribute"/></typeparam>
        /// <typeparam name="U">Class marked with <see cref="TableAttribute"/></typeparam>
        /// <returns>This instance</returns>
        public QueryBuilder InnerJoin<T, U>(string t_on, string u_on = "id")
        {
            var tableDetails = _mainTable.TableType == typeof(U) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(U));
            if (tableDetails == null)
                throw new ArgumentException($"{typeof(U)} has not been added to query.");
            return InnerJoin<T>(t_on, u_on, tableDetails);
        }
        
        private QueryBuilder InnerJoin<T>(string t_on, string tableDetailsOn, TableDetail tableDetails)
        {
            var attributes = typeof(T).GetCustomAttributes(typeof(TableAttribute), true);

            if (attributes.Length == 0)
                throw new ArgumentException($"{typeof(T)} does not have attribute TableAttribute");
            var tableAttr = (TableAttribute)attributes.GetValue(0);
            if (tableAttr == null) throw new ArgumentException("Type does not have a TableAttribute");

            var ta = new JoinTableDetail()
            {
                TableName = tableAttr.Name,
                TableType = typeof(T)
            };
            ta.TableAlias = $"{ta.TableName.Substring(0, 2)}{_joinTableDetails.Count + 2}";
            ta.JoinOn = $"{ta.TableAlias}.{t_on} = {tableDetails.TableAlias}.{tableDetailsOn}";
            _joinTableDetails.Add(ta);
            return this;
        }

        /// <summary>
        /// Adds a <c>WHERE, OR</c> condition for each value in the provided array of values.
        /// </summary>
        /// <param name="fieldName">Name of column, as defined in <see cref="ColumnAttribute"/></param>
        /// <param name="operand"><see cref="Operand"/></param>
        /// <param name="values">Array of values to select</param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>, and added with <see cref="SelectFrom{T}()"/> or <see cref="InnerJoin{T}"/></typeparam>
        /// <returns>This instance</returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder WhereOr<T>(string fieldName, Operand operand, params object[] values)
        {
            var tableDetails = _mainTable.TableType == typeof(T) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(T));
            if (!ColumnNameExists(tableDetails?.TableType, fieldName))
                throw new ArgumentException($"{fieldName} does not exist on table {tableDetails?.TableName}");

            if (tableDetails == null) throw new ArgumentException($"Table {typeof(T)} has not been defined as a select or join table");
            StringBuilder builder = new StringBuilder("(");
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    if (i != 0) builder.Append(" or ");
                    builder.Append($"{tableDetails.TableAlias}.{fieldName} {GetOperand(operand)} '{values[i]}'");
                }
            }

            builder.Append(")");
            whereConditionsList.Add(builder.ToString());

            return this;
        }

        /// <summary>
        /// Add a <c>WHERE</c> clause with the given column name and value. Optionally, include a <see cref="CaseComparison"/> to either ignore
        /// case, or not - selecting <see cref="CaseComparison"/> IGNORECASE will use <c>UPPER</c> on values when doing comparison.
        /// </summary>
        /// <param name="fieldName">Name of column, as defined in <see cref="ColumnAttribute"/></param>
        /// <param name="operand"><see cref="Operand"/></param>
        /// <param name="value">Value to select</param>
        /// <param name="caseComparison">Optional <see cref="CaseComparison"/> argument</param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>, and added with <see cref="SelectFrom{T}()"/> or <see cref="InnerJoin{T}"/></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder Where<T>(string fieldName, Operand operand, object value = null, CaseComparison caseComparison = CaseComparison.NONE)
        {
            var tableDetails = _mainTable.TableType == typeof(T) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(T));
            if (!ColumnNameExists(tableDetails?.TableType, fieldName))
                throw new ArgumentException($"{fieldName} does not exist on table {tableDetails?.TableName}");

            if (tableDetails == null) throw new ArgumentException($"Table {typeof(T)} has not been defined as a select or join table");
            if (operand == Operand.ISNULL)
            {
                whereConditionsList.Add($"{tableDetails.TableAlias}.{fieldName} is null");
            }
            else
            {
                if (caseComparison == CaseComparison.IGNORECASE)
                {
                    whereConditionsList.Add($"UPPER({tableDetails.TableAlias}.{fieldName}) {GetOperand(operand)} UPPER('{value}')");
                }
                else
                {
                    whereConditionsList.Add($"{tableDetails.TableAlias}.{fieldName} {GetOperand(operand)} '{value}'");
                }
            }

            return this;
        }

        /// <summary>
        /// Constructs a  <c>WHERE NOT</c> clause to select only records that DO NOT equal the given value.
        /// </summary>
        /// <param name="fieldName">Name of column, as defined in <see cref="ColumnAttribute"/></param>
        /// <param name="value">Value to select</param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>, and added with <see cref="SelectFrom{T}()"/> or <see cref="InnerJoin{T}"/></typeparam>
        /// <returns>This instance</returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder WhereNot<T>(string fieldName, object value)
        {
            var tableDetails = _mainTable.TableType == typeof(T) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(T));
            if (!ColumnNameExists(tableDetails?.TableType, fieldName))
                throw new ArgumentException($"{fieldName} does not exist on table {tableDetails?.TableName}");

            if (tableDetails == null) throw new ArgumentException($"Table {typeof(T)} has not been defined as a select or join table");
            whereConditionsList.Add($"NOT {tableDetails.TableAlias}.{fieldName} = '{value}'");
            return this;
        }

        /// <summary>
        /// Constructs a WHERE clause to select only records where the fieldName values are NOT in the supplied list of values.
        /// </summary>
        /// <param name="fieldName">column name on <typeparamref name="T"/></param>
        /// <param name="list">list of values to include</param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>, and added with <see cref="SelectFrom{T}()"/> or <see cref="InnerJoin{T}"/></typeparam>
        /// <returns>This instance</returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder WhereNotIn<T>(string fieldName, params object[] list)
        {
            var tableDetails = _mainTable.TableType == typeof(T) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(T));
            if (!ColumnNameExists(tableDetails?.TableType, fieldName))
                throw new ArgumentException($"{fieldName} does not exist on table {tableDetails?.TableName}");

            if (tableDetails == null) throw new ArgumentException($"Table {typeof(T)} has not been defined as a select or join table");
            string value = String.Empty;
            for (int i = 0; i < list.Length; i++)
            {
                value += (i == (list.Length - 1)) ? $"'{list[i]}'" : $"'{list[i]}',";
            }

            whereConditionsList.Add($"NOT {tableDetails.TableAlias}.{fieldName} IN  ({value})");
            return this;
        }

        /// <summary>
        /// Constructs a WHERE clause to select only records where the fieldName values are BETWEEN the supplied minimum and maximum values.<br></br>
        /// NOTE: This method is ideally for date or integer values (or other similar fields), but currently there are no constraints on this method. Thus,
        /// use your own discretion...
        /// </summary>
        /// <param name="fieldName">column name on <typeparamref name="T"/></param>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>, and added with <see cref="SelectFrom{T}()"/> or <see cref="InnerJoin{T}"/></typeparam>
        /// <returns>This instance</returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder WhereBetween<T>(string fieldName, string minValue, string maxValue)
        {
            var tableDetails = _mainTable.TableType == typeof(T) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(T));
            if (!ColumnNameExists(tableDetails?.TableType, fieldName))
                throw new ArgumentException($"{fieldName} does not exist on table {tableDetails?.TableName}");
            if (tableDetails == null) throw new ArgumentException($"Table {typeof(T)} has not been defined as a select or join table");
            whereConditionsList.Add($"{tableDetails.TableAlias}.{fieldName} between '{minValue}' and '{maxValue}'");
            return this;
        }

        /// <summary>
        /// Adds an <c>ORDER BY</c> clause to this query.
        /// </summary>
        /// <param name="fieldName">column name on <typeparamref name="T"/></param>
        /// <param name="order"><see cref="Order"/></param>
        /// <typeparam name="T">class marked with <see cref="TableAttribute"/>, and added with <see cref="SelectFrom{T}()"/> or <see cref="InnerJoin{T}"/></typeparam>
        /// <returns>This instance</returns>
        /// <exception cref="ArgumentException"></exception>
        public QueryBuilder OrderBy<T>(string fieldName, Order order = Order.ASCENDING)
        {
            var tableDetails = _mainTable.TableType == typeof(T) ? _mainTable : _joinTableDetails.Find(t => t.TableType == typeof(T));
            if (!ColumnNameExists(tableDetails?.TableType, fieldName))
                throw new ArgumentException($"{fieldName} does not exist on table {tableDetails?.TableName}");
            if (tableDetails == null) throw new ArgumentException($"Table {typeof(T)} has not been defined as a select or join table");

            _orderBy = $"order by {tableDetails.TableAlias}.{fieldName} {(order == Order.ASCENDING ? "asc" : "desc")}";
            return this;
        }

        /// <summary>
        /// Adds <c>OFFSET</c> to query.
        /// </summary>
        /// <param name="offset">Integer value of offset</param>
        /// <returns>This instance</returns>
        public QueryBuilder Offset(int? offset)
        {
            _offset = offset == null || offset < 0 ? 0 : (int)offset;
            return this;
        }

        public int? GetOffSet()
        {
            return _offset;
        }

        public int? GetLimit()
        {
            return _limit;
        }

        /// <summary>
        /// Adds <c>LIMIT</c> to query.
        /// </summary>
        /// <param name="limit">Integer value of limit</param>
        /// <returns></returns>
        public QueryBuilder Limit(int? limit)
        {
            _limit = limit;
            return this;
        }

        /// <summary>
        /// Returns a string with the constructed query.<br></br>
        /// Use this method to generate the final SQL string query.
        /// </summary>
        /// <returns>String constructed using previously defined clauses, joins, and fields</returns>
        public string AllFieldsQuery()
        {
            StringBuilder builder = new StringBuilder(BuildQuery());
            builder.Append(_orderBy);
            if (_offset != null) builder.Append($" offset {_offset}");
            if (_limit != null) builder.Append($" limit {_limit}");
            string query= string.IsNullOrEmpty(_distinctOn) 
                ? $"select {BuildFields()} {builder}" 
                : $"select distinct on({_distinctOn}) {_distinctOn}, {BuildFields()} {builder}";

            return query;
        }

        /// <summary>
        /// Returns a <c>SELECT COUNT(*)</c> query.<br></br>
        /// Use this method to generate the a count query with all the given parameters.
        /// </summary>
        /// <returns>String constructed using previously defined clauses, joins, and fields.</returns>
        public string CountQuery()
        {
            string query;
            query = string.IsNullOrEmpty(_distinctOn) 
                ? $"select count(*) {BuildQuery()}" 
                : $"select count(distinct {_distinctOn}) {BuildQuery()}";

            return query;
        }

        /// <summary>
        /// Returns a <c>SELECT COUNT(DISTINCT({COLUMN_NAME}))</c> query.
        /// </summary>
        /// <param name="columnName">column name on any previously defined tables</param>
        /// <returns>String query constructed using previously defined clauses, joins, and fields, selecting a distinct count.</returns>
        /// <exception cref="ArgumentException"></exception>
        public string DistinctCountQuery(string columnName)
        {
            //first find the table
            string alias = ColumnNameExists(_mainTable.TableType, columnName)
                ? _mainTable.TableAlias
                : _joinTableDetails.Find(jt => ColumnNameExists(jt.TableType, columnName))?.TableAlias;

            if (alias.IsNullOrEmpty())
                throw new ArgumentException($"{columnName} could not be found on any tables");

            return $"select count(distinct({alias}.{columnName})) {BuildQuery()}";
        }

        private string BuildQuery()
        {
            StringBuilder builder = new StringBuilder($"from {_mainTable.TableName} {_mainTable.TableAlias} ");
            if (!_joinTableDetails.IsNullOrEmpty())
            {
                foreach (var join in _joinTableDetails)
                {
                    builder.Append($"join {join.TableName} {join.TableAlias} on {join.JoinOn} ");
                }
            }

            if (!whereConditionsList.IsNullOrEmpty())
            {
                builder.Append(" where ");
                for (int i = 0; i < whereConditionsList.Count; i++)
                {
                    builder.Append($" {whereConditionsList[i]} ");
                    if (i != whereConditionsList.Count - 1) builder.Append(" and ");
                }
            }

            return builder.ToString();
        }

        private string BuildFields()
        {
            StringBuilder builder = new StringBuilder();
            if (_returnFieldsList != null && _returnFieldsList.Count > 0)
            {
                for (int i = 0; i < _returnFieldsList.Count; i++)
                {
                    if (_returnFieldsList[i].ColumnAlias.Equals(_returnFieldsList[i].ColumnName, StringComparison.InvariantCultureIgnoreCase))
                        builder.Append($" {_returnFieldsList[i].TableAlias}.{_returnFieldsList[i].ColumnName}");
                    else
                        builder.Append($" {_returnFieldsList[i].TableAlias}.{_returnFieldsList[i].ColumnName} {_returnFieldsList[i].ColumnAlias}");
                    if (i != _returnFieldsList.Count - 1) builder.Append(", ");
                }
            }
            else
            {
                for (int i = 0; i < _fieldsList.Count; i++)
                {
                    if (_fieldsList[i].ColumnAlias.Equals(_fieldsList[i].ColumnName, StringComparison.InvariantCultureIgnoreCase))
                        builder.Append($" {_fieldsList[i].TableAlias}.{_fieldsList[i].ColumnName}");
                    else
                        builder.Append($" {_fieldsList[i].TableAlias}.{_fieldsList[i].ColumnName} {_fieldsList[i].ColumnAlias}");
                    if (i != _fieldsList.Count - 1) builder.Append(", ");
                }
            }

            return builder.ToString();
        }

        private string GetOperand(Operand operand)
        {
            switch (operand)
            {
                case Operand.LIKE: return "LIKE";
                case Operand.LESS_THAN: return "<";
                case Operand.LESS_THAN_OR_EQUALS: return "<=";
                case Operand.GREATER_THAN: return ">";
                case Operand.GREATER_THAN_OR_EQUALS: return ">=";
                case Operand.EQUALS:
                default: return "=";
            }
        }

        private class TableDetail
        {
            public string TableName { get; set; }
            public Type TableType { get; set; }
            public string TableAlias { get; set; }
        }

        private class JoinTableDetail : TableDetail
        {
            public string JoinOn { get; set; }
        }

        private class TableFieldDetail
        {
            public string ColumnName { get; set; }
            public string ColumnAlias { get; set; }

            public string TableAlias { get; set; }
        }
    }

    public static class ListHelper
    {
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            if (source == null)
                return true;
            else
                return source.Any() == false;
        }
    }

    public enum CaseComparison
    {
        IGNORECASE,
        NONE
    }

    public enum Operand
    {
        EQUALS,
        GREATER_THAN,
        GREATER_THAN_OR_EQUALS,
        LESS_THAN,
        LESS_THAN_OR_EQUALS,
        LIKE,
        ISNULL
    }

    public enum Order
    {
        DESCENDING,
        ASCENDING
    }


    public class QueryTableAttribute : Attribute
    {
        public Type table { get; set; }

        public QueryTableAttribute(Type type)
        {
            table = type;
        }
    }
}