using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ESC_GULEN_OPTIK_Web.Data
{
    /// <summary>
    /// Database connection helper class.
    /// Similar to instructor's DBConnection.cs pattern.
    /// Provides centralized database operations.
    /// </summary>
    public class DBConnection
    {
        private readonly string _connectionString;
        private SqlConnection _con;

        public DBConnection(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("conStr") 
                ?? throw new InvalidOperationException("Connection string 'conStr' not found.");
            _con = new SqlConnection(_connectionString);
        }

        /// <summary>
        /// Executes a SELECT query and returns a DataSet.
        /// Usage: DataSet ds = dbcon.getSelect("SELECT * FROM Staff");
        /// </summary>
        public DataSet getSelect(string sqlstr)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                throw;
            }

            DataSet ds = new DataSet();
            SqlDataAdapter da = new SqlDataAdapter(sqlstr, _connectionString);
            da.Fill(ds);
            _con.Close();
            return ds;
        }

        /// <summary>
        /// Executes a SELECT query with parameters and returns a DataSet.
        /// Usage: DataSet ds = dbcon.getSelectWithParams("SELECT * FROM Staff WHERE StaffID=@id", ("@id", 1));
        /// </summary>
        public DataSet getSelectWithParams(string sqlstr, params (string name, object value)[] parameters)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                throw;
            }

            DataSet ds = new DataSet();
            using (SqlCommand cmd = new SqlCommand(sqlstr, _con))
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.name, param.value ?? DBNull.Value);
                }
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(ds);
            }
            _con.Close();
            return ds;
        }

        /// <summary>
        /// Executes an INSERT, UPDATE, or DELETE query.
        /// Usage: bool success = dbcon.execute("DELETE FROM Staff WHERE StaffID=1");
        /// </summary>
        public bool execute(string sqlstr)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                return false;
            }

            try
            {
                SqlCommand exec = new SqlCommand(sqlstr, _con);
                exec.ExecuteNonQuery();
                _con.Close();
            }
            catch (Exception)
            {
                _con.Close();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Executes an INSERT, UPDATE, or DELETE query with parameters.
        /// Usage: bool success = dbcon.executeWithParams("DELETE FROM Staff WHERE StaffID=@id", ("@id", 1));
        /// </summary>
        public bool executeWithParams(string sqlstr, params (string name, object value)[] parameters)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                return false;
            }

            try
            {
                using (SqlCommand exec = new SqlCommand(sqlstr, _con))
                {
                    foreach (var param in parameters)
                    {
                        exec.Parameters.AddWithValue(param.name, param.value ?? DBNull.Value);
                    }
                    exec.ExecuteNonQuery();
                }
                _con.Close();
            }
            catch (Exception)
            {
                _con.Close();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Executes an INSERT query and returns the new ID (SCOPE_IDENTITY).
        /// Usage: int newId = dbcon.executeInsert("INSERT INTO Staff (...) VALUES (...); SELECT SCOPE_IDENTITY();");
        /// </summary>
        public int executeInsert(string sqlstr, params (string name, object value)[] parameters)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                return -1;
            }

            try
            {
                using (SqlCommand exec = new SqlCommand(sqlstr, _con))
                {
                    foreach (var param in parameters)
                    {
                        exec.Parameters.AddWithValue(param.name, param.value ?? DBNull.Value);
                    }
                    var result = exec.ExecuteScalar();
                    _con.Close();
                    return Convert.ToInt32(result);
                }
            }
            catch (Exception)
            {
                _con.Close();
                return -1;
            }
        }

        /// <summary>
        /// Executes a stored procedure without output parameters.
        /// Usage: dbcon.executeStoredProcedure("proc_CreateSale", ("@CustomerID", 1), ("@StaffID", 2));
        /// </summary>
        public bool executeStoredProcedure(string procedureName, params (string name, object value)[] parameters)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                return false;
            }

            try
            {
                using (SqlCommand cmd = new SqlCommand(procedureName, _con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    foreach (var param in parameters)
                    {
                        cmd.Parameters.AddWithValue(param.name, param.value ?? DBNull.Value);
                    }
                    cmd.ExecuteNonQuery();
                }
                _con.Close();
            }
            catch (Exception)
            {
                _con.Close();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Executes a stored procedure with an output parameter.
        /// Usage: int result = dbcon.executeStoredProcedureWithOutput("deleteDepartment", "@result", ("@deptCode", "CS"));
        /// </summary>
        public int executeStoredProcedureWithOutput(string procedureName, string outputParamName, params (string name, object value)[] parameters)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                return -1;
            }

            try
            {
                using (SqlCommand cmd = new SqlCommand(procedureName, _con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    foreach (var param in parameters)
                    {
                        cmd.Parameters.AddWithValue(param.name, param.value ?? DBNull.Value);
                    }
                    cmd.Parameters.Add(new SqlParameter(outputParamName, SqlDbType.Int));
                    cmd.Parameters[outputParamName].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();
                    int result = (int)cmd.Parameters[outputParamName].Value;
                    _con.Close();
                    return result;
                }
            }
            catch (Exception)
            {
                _con.Close();
                return -1;
            }
        }

        /// <summary>
        /// Executes a stored procedure and returns a DataSet.
        /// Usage: DataSet ds = dbcon.getStoredProcedure("proc_SearchProducts", ("@ProductCategory", "FRAME"));
        /// </summary>
        public DataSet getStoredProcedure(string procedureName, params (string name, object value)[] parameters)
        {
            try
            {
                _con.Open();
            }
            catch (Exception)
            {
                _con.Close();
                throw;
            }

            DataSet ds = new DataSet();
            using (SqlCommand cmd = new SqlCommand(procedureName, _con))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.name, param.value ?? DBNull.Value);
                }
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(ds);
            }
            _con.Close();
            return ds;
        }

        /// <summary>
        /// Gets the connection string for direct use.
        /// </summary>
        public string GetConnectionString()
        {
            return _connectionString;
        }

        /// <summary>
        /// Tests the database connection.
        /// </summary>
        public (bool Success, string Message) TestConnection()
        {
            try
            {
                _con.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT DB_NAME()", _con))
                {
                    var dbName = cmd.ExecuteScalar();
                    _con.Close();
                    return (true, $"Connected to: {dbName}");
                }
            }
            catch (Exception ex)
            {
                _con.Close();
                return (false, ex.Message);
            }
        }
    }
}

